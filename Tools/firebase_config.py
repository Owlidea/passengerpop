#!/usr/bin/env python3
"""Turns Firebase's google-services.json into the Android string resources the Firebase SDK reads at start-up.

Firebase normally does this with its Gradle plugin. The game adds the native SDK without that plugin (see
Docs/ANALYTICS.md), so this script writes the same strings into the game's own Android library:

    Config/firebase/google-services.json   (downloaded from the Firebase console, not in the repository)
 -> Assets/Plugins/Android/MetroFirebase.androidlib/res/values/google-services.xml

Run it again whenever the file from the console changes:

    python3 Tools/firebase_config.py

It also checks that the iOS file (Config/firebase/GoogleService-Info.plist) is for this game.
"""
import json
import os
import plistlib
import sys
from xml.sax.saxutils import escape

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PACKAGE = "com.owlidea.metrosort"
SOURCE = os.path.join(ROOT, "Config", "firebase", "google-services.json")
IOS = os.path.join(ROOT, "Config", "firebase", "GoogleService-Info.plist")
TARGET = os.path.join(ROOT, "Assets", "Plugins", "Android", "MetroFirebase.androidlib", "res", "values",
                      "google-services.xml")


def android_strings(config, package):
    """The strings Firebase's Gradle plugin would generate for this app."""
    client = None
    for candidate in config.get("client", []):
        info = candidate.get("client_info", {})
        if info.get("android_client_info", {}).get("package_name") == package:
            client = candidate
    if client is None:
        raise SystemExit("google-services.json has no Android app with the package name " + package)
    project = config.get("project_info", {})
    keys = client.get("api_key", [])
    api_key = keys[0].get("current_key") if keys else None
    web_client = None
    for oauth in client.get("oauth_client", []):
        if oauth.get("client_type") == 3:
            web_client = oauth.get("client_id")
    strings = [
        ("google_app_id", client["client_info"]["mobilesdk_app_id"]),
        ("gcm_defaultSenderId", project.get("project_number")),
        ("project_id", project.get("project_id")),
        ("google_storage_bucket", project.get("storage_bucket")),
        ("firebase_database_url", project.get("firebase_url")),
        ("google_api_key", api_key),
        ("google_crash_reporting_api_key", api_key),
        ("default_web_client_id", web_client),
    ]
    return [(name, value) for name, value in strings if value]


def main():
    if not os.path.exists(SOURCE):
        raise SystemExit("Missing " + os.path.relpath(SOURCE, ROOT) + ": download google-services.json from the "
                         "Firebase console (Project settings > Your apps > Android) and put it there.")
    with open(SOURCE, encoding="utf-8") as f:
        strings = android_strings(json.load(f), PACKAGE)
    lines = ['<?xml version="1.0" encoding="utf-8"?>',
             "<!-- Written by Tools/firebase_config.py from Config/firebase/google-services.json. Do not edit. -->",
             "<resources>"]
    for name, value in strings:
        lines.append('    <string name="%s" translatable="false">%s</string>' % (name, escape(str(value))))
    lines.append("</resources>")
    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with open(TARGET, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    # Names only: the values are identifiers of the Firebase project and stay out of logs.
    print("Android: wrote %s (%s)" % (os.path.relpath(TARGET, ROOT), ", ".join(name for name, _ in strings)))

    if not os.path.exists(IOS):
        print("iOS: " + os.path.relpath(IOS, ROOT) + " is missing; iOS builds will send no analytics.")
        return 0
    with open(IOS, "rb") as f:
        plist = plistlib.load(f)
    if plist.get("BUNDLE_ID") != PACKAGE:
        raise SystemExit("GoogleService-Info.plist is for bundle id %r, not %s" % (plist.get("BUNDLE_ID"), PACKAGE))
    if not plist.get("GOOGLE_APP_ID"):
        raise SystemExit("GoogleService-Info.plist has no GOOGLE_APP_ID")
    print("iOS: " + os.path.relpath(IOS, ROOT) + " is for " + PACKAGE + "; the build copies it into the app.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
