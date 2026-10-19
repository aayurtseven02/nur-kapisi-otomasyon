import os,json,sys,re,unicodedata,difflib,io,wave,math,functools
from pathlib import Path

def safe_path(value):
    absolute=os.path.abspath(value)
    if os.name=='nt' and not absolute.startswith('\\\\?\\'):
        absolute='\\\\?\\'+absolute
    return Path(absolute)

def tokens(text):
    text=text.lower().replace('ı','i')
    text=''.join(c for c in unicodedata.normalize('NFD',text) if unicodedata.category(c)!='Mn')
    return re.findall(r'[^\W_]+',text,re.UNICODE)

def align_words(words,texts,duration,merge=False):
    heard=[];previous_word=0
    for word in words:
        start,end=float(word['Start']),float(word['End'])
        if not all(map(math.isfinite,[start,end])) or start<previous_word-.15 or end<start or end>duration+.15:
            raise RuntimeError('Kelime zamanları geçersiz.')
        previous_word=start
        for token in tokens(word['Word']):heard.append((token,start,end))
    source=[];bounds=[]
    for text in texts:
        begin=len(source);source.extend(tokens(text));bounds.append((begin,len(source)))
    if not source or not heard:raise RuntimeError('Konuşma algılanamadı.')
    matcher=difflib.SequenceMatcher(None,source,[w[0] for w in heard],autojunk=False)
    anchors={}
    for block in matcher.get_matching_blocks():
        for k in range(block.size):anchors[block.a+k]=block.b+k
    if len(anchors)<len(source)*.65:raise RuntimeError('Konuşma ile metin yeterince eşleşmedi; yanlış senkron kullanılmadı.')
    def candidate(first,last,previous):
        begin,end=bounds[first][0],bounds[last][1]
        matches=[anchors[j] for j in range(begin,end) if j in anchors]
        if not matches or len(matches)<max(1,int((end-begin)*.35)):return None
        start=max(previous,heard[min(matches)][1]);stop=heard[max(matches)][2]
        if last>first:
            # Share a verified interval rather than inventing uncertain word times.
            if len(' '.join(texts[first:last+1]))>180:return None
            first_matches=[anchors[j] for j in range(*bounds[first]) if j in anchors]
            last_matches=[anchors[j] for j in range(*bounds[last]) if j in anchors]
            if not first_matches:start=previous if first else heard[0][1]
            if not last_matches:stop=heard[-1][2] if last==len(bounds)-1 else next((heard[anchors[j]][1] for j in range(end,len(source)) if j in anchors),stop)
            if last==len(bounds)-1:stop=heard[-1][2]
            spoken=' '.join(w[0] for w in heard if w[1]>=start-.1 and w[2]<=stop+.1)
            expected=' '.join(source[begin:end])
            if stop-start>18 or difflib.SequenceMatcher(None,expected,spoken,autojunk=False).ratio()<.5:return None
        if stop<=start:return None
        row={'Id':first,'Start':start,'End':stop}
        if last>first:row['LastId']=last
        return row
    @functools.lru_cache(maxsize=4096)
    def solve(first,previous):
        if first==len(bounds):return []
        for last in range(first,min(len(bounds),first+(4 if merge else 1))):
            row=candidate(first,last,previous)
            if row is None:continue
            rest=solve(last+1,row['End'])
            if rest is not None:return [row]+rest
        return None
    rows=solve(0,0)
    if rows is None:raise RuntimeError('Altyazı satırı seste doğrulanamadı; dar ve doğrulanmış bir aralık bulunamadı.')
    if rows[-1]['End']<duration-max(3,duration*.08):raise RuntimeError('Konuşmanın sonu eşlenemedi.')
    return {'Captions':rows,'Words':words,'MatchedWords':len(anchors),'SourceWords':len(source),'MergedLines':sum(r.get('LastId',r['Id'])-r['Id'] for r in rows)}

def main():
    os.environ['HF_HUB_OFFLINE']='1';os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
    request=json.loads(safe_path(sys.argv[1]).read_text(encoding='utf-8-sig'));target=safe_path(sys.argv[2]);word_cache=safe_path(sys.argv[2]+'.words.json')
    def save(words):
        try:result=align_words(words,request['Texts'],request['Duration'])
        except RuntimeError:result=align_words(words,request['Texts'],request['Duration'],True)
        target.write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
        print('Kelime zamanları eşlendi',result['MatchedWords'],'/',result['SourceWords'],flush=True)
        if result['MergedLines']:print('ASR belirsizliği olan komşu altyazılar doğrulanmış ses aralığında birleştirildi; özgün ses değişmedi.',flush=True)
    if not request.get('Transcribe') and word_cache.exists():
        try:save(json.loads(word_cache.read_text(encoding='utf-8-sig'))['Words']);return
        except (RuntimeError,ValueError,KeyError,OSError,TypeError):pass
    from faster_whisper import WhisperModel
    model=WhisperModel(request.get('Model') or os.environ.get('ANKA_ALIGNMENT_MODEL',str(Path(__file__).parent/'model')),device='cpu',compute_type='int8',cpu_threads=4,local_files_only=True)
    if request.get('Transcribe'):
        segments,info=model.transcribe(request['Audio'],language='tr',word_timestamps=True,beam_size=5,condition_on_previous_text=False)
        words=[{'Word':w.word.strip(),'Start':w.start,'End':w.end} for segment in segments for w in segment.words or []]
        if not words:raise RuntimeError('Konuşma algılanamadı.')
        target.write_text(json.dumps({'Words':words},ensure_ascii=False),encoding='utf-8');return
    def attempt(use_prompt,chunked=False):
        words=[]
        def read(stream,offset=0):
            segments,info=model.transcribe(stream,language='tr',word_timestamps=True,beam_size=5,condition_on_previous_text=False,vad_filter=False,initial_prompt=' '.join(request['Texts']) if use_prompt else None)
            for segment in segments:words.extend({'Word':w.word.strip(),'Start':w.start+offset,'End':w.end+offset} for w in segment.words or [])
        if chunked:
            print('Ses kısa parçalarla yeniden inceleniyor; özgün ses korunuyor.',flush=True)
            with wave.open(str(safe_path(request['Audio'])),'rb') as audio:
                params=audio.getparams();step=params.framerate*10
                for first in range(0,params.nframes,step):
                    audio.setpos(first);content=audio.readframes(min(step,params.nframes-first));clip=io.BytesIO()
                    with wave.open(clip,'wb') as writer:writer.setparams(params);writer.writeframes(content)
                    clip.seek(0);read(clip,first/params.framerate)
        else:
            with open(safe_path(request['Audio']),'rb') as audio_stream:read(audio_stream)
        word_cache.write_text(json.dumps({'Words':words},ensure_ascii=False),encoding='utf-8');save(words)
    try:attempt(False)
    except RuntimeError:
        try:attempt(True)
        except RuntimeError:attempt(False,True)

if __name__=='__main__':main()
