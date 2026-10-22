namespace AnkaStudio;
public static class AnimationVideoTests
{
    public static async Task Run(string report)
    {
        var checks=new List<string>();
        if(!StoryboardSections.SentenceBoundaries("Bir adam yürüdü. Sonra durup baktı. Elmayı aldı. Sahibine doğru koştu. Kapıyı çaldı.",[2,5,7,9,13]).SequenceEqual(new[]{3,6,8,11,13}))throw new Exception("Sahneler cümle ortasında kesildi.");
        checks.Add("PASS yeterli cümle varken sahne sınırları cümle sonlarına oturur");
        var castFixture=StoryDirector.ReadCast("{\"Characters\":[{\"Id\":0,\"Role\":\"protagonist\",\"Description\":\"Young adult with dark hair wearing an ivory tunic.\"},{\"Id\":1,\"Role\":\"farmer\",\"Description\":\"Older adult with grey hair wearing a brown robe.\"}]}");
        if(castFixture.Count!=2)throw new Exception("Karakterler ayrışmadı.");bool unknown=false;try{StoryDirector.ReadScene("{\"Prompt\":\"A cinematic cartoon of one adult holding an apple by the river.\",\"CharacterIds\":[2]}",2);}catch(AiContentException){unknown=true;}if(!unknown)throw new Exception("Listede olmayan karakter kabul edildi.");
        var frame=StoryDirector.ReadFrame("{\"Subjects\":[{\"Id\":1,\"Action\":\"Standing beside the apple tree\"}],\"Setting\":\"A sunny orchard with red apples\",\"Lighting\":\"Soft afternoon sunlight\",\"Camera\":\"Medium portrait shot\"}",castFixture,"3D cartoon");
        if(!frame.Ids.SequenceEqual(new[]{1})||!frame.Prompt.Contains(castFixture[1].Description)||frame.Prompt.Contains(castFixture[0].Description))throw new Exception("Sahne yazısı ve referans listesi aynı görünen kişilerden kurulmadı.");
        var referenceGraph=LocalCoverEngine.Prepare(Path.Combine(AppContext.BaseDirectory,"tools","workflows","anka-flux2.json"),"A person holds an apple.",true);LocalCoverEngine.ApplyReference(referenceGraph,"cast.png",42,2,[1],900,false,512);
        if(referenceGraph["anka_reference_subject"]!["inputs"]!["x"]!.GetValue<int>()!=450||referenceGraph["anka_reference_subject"]!["inputs"]!["height"]!.GetValue<int>()!=512)throw new Exception("Tek kişilik sahne bütün karakterleri referans aldı.");
        var identityEpisode=new Episode{Topic="Hikâye"};string beforeCast=StoryImages.Identity(identityEpisode);identityEpisode.AiStoryCastPrompt="A different cast";if(StoryImages.Identity(identityEpisode)==beforeCast)throw new Exception("Değişen karakter kadrosu eski referansı kullandı.");
        checks.Add("PASS karakter kimlikleri, tek kişi referans kırpması ve kadro değişiminde önbellek yenileme");
        if(KeylessWebResearch.Article("<header>menü</header><div class='entry-content'><p>Gerçek olay.</p><div>Sonuç.</div></div><footer>ilgili haber</footer>")!="Gerçek olay. Sonuç.")throw new Exception("Makale yerine menü metni okundu.");
        checks.Add("PASS araştırma okuyucusu makaleyi menü ve ilgili haberlerden ayırır");
        bool repetitionRejected=false;try{AutonomousProduction.ReadDraft("{\"Text\":\"Genç adam nehir kıyısında bir elma buldu ve sahibini aradı. Genç adam nehir kıyısında bir elma buldu ve sahibini aradı. Genç adam nehir kıyısında bir elma buldu ve sahibini aradı.\"}");}catch(AiContentException){repetitionRejected=true;}if(!repetitionRejected)throw new Exception("Tekrarlı anlatım kabul edildi.");
        checks.Add("PASS kelime bütçesini tekrarlarla dolduran anlatım reddedilir");
        if(!Equals(LocalAiSchema.ForRequest("Return Queries","topic",true,null,null),"json")||!Equals(LocalAiSchema.ForRequest("Return Text","topic",true,null,null),"json")||LocalAiSchema.ForRequest("video plan","{}",true,1,false) is string)throw new Exception("Genel JSON isteğine senaryo şeması uygulandı.");
        checks.Add("PASS araştırma/metin/hareket JSON istekleri yanlış senaryo şemasına zorlanmaz");
        var automatic=new Episode{Topic="Bir elma ve dürüstlük"};var flow=new CreationFlow{Job=new ProductionJob{PublishMode="review"}};
        AutonomousProduction.Configure(automatic,flow);
        if(!flow.AiVisuals||flow.StockVisuals||flow.MixedSceneVideos!=true||flow.SceneVideoModel!=AnimationVideo.Model||!automatic.AiStoryAutonomous||!automatic.AiStoryLocal||flow.Subtitles!=true)throw new Exception("Tam otonom film varsayılanları eksik.");
        string signature=flow.Signature;flow.SceneVideoModel=ImportedLtx23.Model;if(flow.Signature==signature)throw new Exception("Motor değişimi hareket planını yenilemiyor.");
        var manual=new CreationFlow{OwnVisuals=true,Files=["own.png"],TextMode="own",OwnText="Kullanıcının kendi anlatımı."};AutonomousProduction.Configure(new Episode{Topic="Kendi görüntülerim"},manual);
        if(manual.AiVisuals||manual.MixedSceneVideos==true)throw new Exception("Kullanıcı görüntü seçimi değiştirildi.");
        checks.Add("PASS otonom AI referans + her sahnede video + stoksuz üretim; kendi dosya seçimi korunur");
        for(int success=0;success<5;success++){
            var calls=new List<(bool Wan,int Attempt)>();bool failed=false;
            try{await AnimationVideo.RunFallback((wan,attempt)=>{calls.Add((wan,attempt));return calls.Count==success?Task.CompletedTask:Task.FromException(new InvalidOperationException("fixture"));},()=>Task.CompletedTask,_=>{},CancellationToken.None);}catch(InvalidOperationException){failed=true;}
            int expected=success==0?4:success;
            if(calls.Count!=expected||failed!=(success==0)||calls.Where((v,i)=>v.Wan!=(i>=2)||v.Attempt!=i%2).Any())throw new Exception("Yedek sırası/deneme sınırı bozuldu.");
        }
        checks.Add("PASS LTX iki deneme, Wan iki deneme, başarı sonrası durma");
        using var cancel=new CancellationTokenSource();int attempts=0;
        try{await AnimationVideo.RunFallback((w,a)=>{attempts++;cancel.Cancel();throw new OperationCanceledException(cancel.Token);},()=>Task.CompletedTask,_=>{},cancel.Token);throw new Exception("İptal yutuldu");}catch(OperationCanceledException){}
        if(attempts!=1)throw new Exception("İptal sonrası motor denendi");checks.Add("PASS iptal yedek motora geçmez");
        foreach(bool wan in new[]{false,true})foreach(bool vertical in new[]{false,true}){
            var g=AnimationVideo.Graph(wan,"fixture.png","A young man carefully raises an apple.",42,vertical);
            string id=wan?"8":"10";
            if(g[id]!["inputs"]!["width"]!.GetValue<int>()!=(vertical?480:832)||g[wan?"16":"21"]!["class_type"]!.GetValue<string>()!="SaveVideo")throw new Exception("Profil biçimi geçersiz");
        }
        checks.Add("PASS iki motorun dikey/yatay profili ve video çıktısı");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))!);File.WriteAllLines(report,checks);
    }
}
