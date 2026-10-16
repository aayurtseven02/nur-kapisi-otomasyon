import os,json,sys,re,unicodedata,difflib,io,wave
from pathlib import Path
def safe_path(value):
    absolute=os.path.abspath(value)
    if os.name=='nt' and not absolute.startswith('\\\\?\\'):
        absolute='\\\\?\\'+absolute
    return Path(absolute)
os.environ['HF_HUB_OFFLINE']='1'
os.environ['HF_HUB_DISABLE_TELEMETRY']='1'
from faster_whisper import WhisperModel
request=json.loads(safe_path(sys.argv[1]).read_text(encoding='utf-8-sig'))
model=WhisperModel(os.environ.get('ANKA_ALIGNMENT_MODEL',str(Path(__file__).parent/'model')),device='cpu',compute_type='int8',cpu_threads=4,local_files_only=True)
def attempt(use_prompt,chunked=False):
    words=[]
    def read(stream,offset=0):
        segments,info=model.transcribe(stream,language='tr',word_timestamps=True,beam_size=5,condition_on_previous_text=False,vad_filter=False,initial_prompt=" ".join(request["Texts"]) if use_prompt else None)
        for segment in segments:
            words.extend({'Word':w.word.strip(),'Start':w.start+offset,'End':w.end+offset} for w in segment.words or [])
    if chunked:
        print('Sesin başı kısa parçalarla yeniden inceleniyor; özgün ses korunuyor.',flush=True)
        with wave.open(str(safe_path(request['Audio'])),'rb') as audio:
            params=audio.getparams(); step=params.framerate*10
            for first in range(0,params.nframes,step):
                audio.setpos(first); content=audio.readframes(min(step,params.nframes-first))
                clip=io.BytesIO()
                with wave.open(clip,'wb') as writer:
                    writer.setparams(params);writer.writeframes(content)
                clip.seek(0);read(clip,first/params.framerate)
    else:
        with open(safe_path(request['Audio']),'rb') as audio_stream:read(audio_stream)
    safe_path(sys.argv[2]+'.words.json').write_text(json.dumps({'Words':words},ensure_ascii=False),encoding='utf-8')
    def tokens(text):
        text=text.lower().replace('ı','i')
        text=''.join(c for c in unicodedata.normalize('NFD',text) if unicodedata.category(c)!='Mn')
        return re.findall(r'[^\W_]+',text,re.UNICODE)
    heard=[]
    for word in words:
        for token in tokens(word['Word']):heard.append((token,word['Start'],word['End']))
    source=[];bounds=[]
    for text in request['Texts']:
        begin=len(source);source.extend(tokens(text));bounds.append((begin,len(source)))
    matcher=difflib.SequenceMatcher(None,source,[w[0] for w in heard],autojunk=False)
    anchors={}
    for block in matcher.get_matching_blocks():
        for k in range(block.size):anchors[block.a+k]=block.b+k
    if len(anchors)<len(source)*.65:raise RuntimeError('Konuşma ile metin yeterince eşleşmedi; yanlış senkron kullanılmadı.')
    rows=[];previous=0
    for i,(begin,end) in enumerate(bounds):
        matches=[anchors[j] for j in range(begin,end) if j in anchors]
        if not matches or len(matches)<max(1,int((end-begin)*.35)):raise RuntimeError('Altyazı satırı seste doğrulanamadı: '+str(i+1))
        start=max(previous,heard[min(matches)][1]);stop=heard[max(matches)][2]
        if stop<=start:raise RuntimeError('Kelime zamanı sıralı değil.')
        rows.append({'Id':i,'Start':start,'End':stop});previous=stop
    if rows[-1]['End']<request['Duration']-max(3,request['Duration']*.08):raise RuntimeError('Konuşmanın sonu eşlenemedi.')
    safe_path(sys.argv[2]).write_text(json.dumps({'Captions':rows,'Words':words,'MatchedWords':len(anchors),'SourceWords':len(source)},ensure_ascii=False),encoding='utf-8')
    print('Kelime zamanları eşlendi',len(anchors),'/',len(source),flush=True)
    
    

try:
    attempt(False)
except RuntimeError:
    try:
        attempt(True)
    except RuntimeError:
        attempt(False,chunked=True)
