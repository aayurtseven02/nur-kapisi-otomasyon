#!/usr/bin/env python3
"""
İLK KURULUM SCRIPTİ — SADECE BİR KEZ, KENDİ BİLGİSAYARINIZDA ÇALIŞTIRIN.

Ne yapar:
  1) Google Cloud Console'dan indirdiğiniz client_secret.json dosyasını kullanarak
     tarayıcınızda YouTube hesabınızdan izin istenir.
  2) İzin verdikten sonra bir "refresh_token" üretilir ve ekrana yazdırılır.
  3) Bu refresh_token'ı GitHub repo secrets'a (YT_REFRESH_TOKEN) kaydedersiniz.
     Böylece GitHub Actions, sizin bilgisayarınız kapalıyken bile sizin adınıza
     YouTube'a video yükleyebilir.

Kullanım:
  python3 scripts/oauth_setup.py --client-secrets client_secret.json

client_secret.json nasıl alınır -> README.md "YouTube API Kurulumu" bölümüne bakın.
"""
import argparse
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from google_auth_oauthlib.flow import InstalledAppFlow

SCOPES = ["https://www.googleapis.com/auth/youtube.upload",
          "https://www.googleapis.com/auth/youtube"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--client-secrets", required=True, help="Google Cloud Console'dan indirilen JSON dosyası")
    args = parser.parse_args()

    flow = InstalledAppFlow.from_client_secrets_file(args.client_secrets, SCOPES)
    creds = flow.run_local_server(port=0)

    print("\n" + "=" * 70)
    print("KURULUM TAMAMLANDI! Aşağıdaki bilgileri GitHub repo > Settings > ")
    print("Secrets and variables > Actions bölümüne EKLEYİN:")
    print("=" * 70)
    print(f"YT_CLIENT_ID      = {creds.client_id}")
    print(f"YT_CLIENT_SECRET  = {creds.client_secret}")
    print(f"YT_REFRESH_TOKEN  = {creds.refresh_token}")
    print("=" * 70)
    print("Bu bilgileri KİMSEYLE PAYLAŞMAYIN, herkese açık yerlere YAZMAYIN.")


if __name__ == "__main__":
    main()
