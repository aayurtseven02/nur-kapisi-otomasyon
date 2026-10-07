"""
YouTube Data API v3 ile otomatik yükleme modülü.

Kimlik doğrulama: OAuth2 (ücretsiz). İlk kurulumda scripts/oauth_setup.py ile BİR
KEZ tarayıcı üzerinden izin verilir ve bir "refresh token" elde edilir. Bu refresh
token GitHub Actions secrets içine kaydedilir; sonraki tüm otomatik çalıştırmalarda
tarayıcıya gerek kalmadan, sessizce yeni erişim token'ı üretilir.

Zamanlanmış yayın: Video "private" olarak yüklenir + status.publishAt alanına
ISO8601 UTC zaman verilir -> YouTube, o saat geldiğinde videoyu otomatik olarak
HERKESE AÇIK yapar. Bu, "programa göre saati gelince yayına alma" ihtiyacını
YouTube'un kendi altyapısıyla, bizim sunucumuz o an açık olmasa bile karşılar.
"""
from __future__ import annotations
import os
import pickle
from dataclasses import dataclass
from typing import List, Optional

from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from google.auth.transport.requests import Request
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload

SCOPES = ["https://www.googleapis.com/auth/youtube.upload",
          "https://www.googleapis.com/auth/youtube"]


def get_authenticated_service(client_secrets_file: Optional[str] = None,
                                token_pickle_path: str = "token.pickle",
                                refresh_token: Optional[str] = None,
                                client_id: Optional[str] = None,
                                client_secret: Optional[str] = None):
    """
    İki çalışma modu:
      1) Yerel ilk kurulum: client_secrets_file verilir, tarayıcı açılır (oauth_setup.py bunu kullanır)
      2) Otomasyon (GitHub Actions): refresh_token + client_id + client_secret env'den okunur,
         tarayıcıya gerek kalmadan credentials oluşturulur.
    """
    creds = None
    if refresh_token and client_id and client_secret:
        creds = Credentials(
            token=None,
            refresh_token=refresh_token,
            token_uri="https://oauth2.googleapis.com/token",
            client_id=client_id,
            client_secret=client_secret,
            scopes=SCOPES,
        )
        creds.refresh(Request())
    else:
        if os.path.exists(token_pickle_path):
            with open(token_pickle_path, "rb") as f:
                creds = pickle.load(f)
        if not creds or not creds.valid:
            if creds and creds.expired and creds.refresh_token:
                creds.refresh(Request())
            else:
                flow = InstalledAppFlow.from_client_secrets_file(client_secrets_file, SCOPES)
                creds = flow.run_local_server(port=0)
            with open(token_pickle_path, "wb") as f:
                pickle.dump(creds, f)

    return build("youtube", "v3", credentials=creds)


@dataclass
class UploadResult:
    video_id: str
    url: str


def upload_video(
    youtube,
    video_file_path: str,
    title: str,
    description: str,
    tags: List[str],
    category_id: str = "22",
    privacy_status: str = "private",
    publish_at_iso_utc: Optional[str] = None,
    made_for_kids: bool = False,
    thumbnail_path: Optional[str] = None,
) -> UploadResult:
    body = {
        "snippet": {
            "title": title[:100],
            "description": description[:5000],
            "tags": tags[:500],
            "categoryId": category_id,
        },
        "status": {
            "privacyStatus": privacy_status,
            "selfDeclaredMadeForKids": made_for_kids,
        },
    }
    if publish_at_iso_utc and privacy_status == "private":
        body["status"]["publishAt"] = publish_at_iso_utc

    media = MediaFileUpload(video_file_path, chunksize=1024 * 1024 * 4, resumable=True, mimetype="video/mp4")
    request = youtube.videos().insert(part="snippet,status", body=body, media_body=media)

    response = None
    while response is None:
        status, response = request.next_chunk()
        if status:
            print(f"[youtube_upload] yükleniyor: %{int(status.progress() * 100)}")

    video_id = response["id"]

    # Thumbnail ayrı bir API çağrısıdır ve BAŞARISIZ olabilir (örn. kanal
    # telefonla doğrulanmamışsa YouTube thumbnails().set()'i reddeder).
    # ESKİ kod burada hatayı yukarı fırlatıyordu; video zaten yüklenmiş
    # olmasına rağmen scheduler görevi 'failed' işaretleyip bir sonraki
    # koşuda AYNI VİDEOYU TEKRAR yüklüyordu (YouTube'da mükerrer video).
    # Thumbnail başarısızlığı artık video kimliğini kaybetmeye yetecek kadar
    # ciddi sayılmaz: uyarı verilir, yükleme başarılı kabul edilir.
    if thumbnail_path and os.path.exists(thumbnail_path):
        try:
            youtube.thumbnails().set(
                videoId=video_id,
                media_body=MediaFileUpload(thumbnail_path, mimetype="image/jpeg"),
            ).execute()
        except Exception as e:
            print(
                f"[youtube_upload] UYARI: thumbnail yüklenemedi ({video_id}): {e}\n"
                f"  Video başarıyla yüklenmiş durumda; kapak görselini "
                f"YouTube Studio'dan elle ekleyebilirsiniz."
            )

    return UploadResult(video_id=video_id, url=f"https://www.youtube.com/watch?v={video_id}")
