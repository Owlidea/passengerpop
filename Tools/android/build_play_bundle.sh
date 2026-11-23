#!/bin/bash
# Builds the Google Play bundle (Builds/Android/MetroSort.aab) signed with Passenger Pop's upload key.
#
# On the first run it creates the upload key at ~/Keys/passengerpop-upload.keystore (override with
# METROSORT_KEYSTORE). The password is typed here, handed to Unity only through environment variables, and
# scrubbed afterwards from the build log and Unity's generated Gradle files. Keep the keystore and its password
# safe (a backup plus a password manager): every Google Play update must be signed with the same upload key.
#
# Usage: bash Tools/android/build_play_bundle.sh   (Unity must be closed)
set -euo pipefail
cd "$(dirname "$0")/../.."

UNITY_VERSION=6000.6.9f1
UNITY="/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity"
JDK="/Applications/Unity/Hub/Editor/$UNITY_VERSION/PlaybackEngines/AndroidPlayer/OpenJDK/bin"
KEYSTORE="${METROSORT_KEYSTORE:-$HOME/Keys/passengerpop-upload.keystore}"
ALIAS="${METROSORT_KEY_ALIAS:-passengerpop}"
LOG=Logs/android_play_build.log

if pgrep -qf "Unity.app/Contents/MacOS/Unity"; then
  echo "Unity is open. Close it and run this again (a batch build cannot share the project)."
  exit 1
fi

if [ ! -f "$KEYSTORE" ]; then
  echo "No upload key yet. Creating $KEYSTORE"
  echo "Pick a strong password (at least 6 characters) and save it in your password manager."
  mkdir -p "$(dirname "$KEYSTORE")"
  "$JDK/keytool" -genkeypair -keystore "$KEYSTORE" -storetype PKCS12 -alias "$ALIAS" \
    -keyalg RSA -keysize 2048 -validity 10000 \
    -dname "CN=Owlidea Bilisim Teknolojileri Ltd. Sti., O=Owlidea, L=Ankara, C=TR"
  chmod 600 "$KEYSTORE"
  echo "Upload key created. Back up $KEYSTORE somewhere safe."
fi

read -r -s -p "Upload key password: " METROSORT_KEYSTORE_PASS
echo
export METROSORT_KEYSTORE="$KEYSTORE" METROSORT_KEY_ALIAS="$ALIAS" METROSORT_KEYSTORE_PASS
export METROSORT_KEY_PASS="$METROSORT_KEYSTORE_PASS"

if ! "$JDK/keytool" -list -keystore "$KEYSTORE" -alias "$ALIAS" -storepass:env METROSORT_KEYSTORE_PASS >/dev/null 2>&1; then
  echo "That password does not open $KEYSTORE (alias $ALIAS)."
  exit 1
fi

scrub() {
  # Unity writes the signing passwords into its generated Gradle project (escaped, so a plain text search can
  # miss them): mask the signing fields by name, then any literal copy left in the build log.
  local files
  files=$(grep -rlE "storePassword|keyPassword|signing\.(store|key)\.password" Library/Bee/Android 2>/dev/null || true)
  if [ -n "$files" ]; then
    echo "$files" | while IFS= read -r f; do
      perl -pi -e 's/((?:storePassword|keyPassword)\s*=?\s*)([\x27"])(?:\\.|(?!\2).)*\2/$1$2***$2/g; s/(signing\.(?:store|key)\.password\s*=\s*).*/$1***/g' "$f"
    done
  fi
  if [ -f "$LOG" ] && grep -qF -- "$METROSORT_KEYSTORE_PASS" "$LOG"; then
    perl -pi -e 'BEGIN { $p = $ENV{METROSORT_KEYSTORE_PASS} } s/\Q$p\E/***/g' "$LOG"
  fi
}
trap scrub EXIT

echo "Building the bundle (10-20 minutes). Log: $LOG"
"$UNITY" -batchmode -quit -projectPath "$PWD" -buildTarget Android \
  -executeMethod MetroSort.EditorTools.MetroSortBuild.BuildAndroid -logFile "$LOG"

AAB=Builds/Android/MetroSort.aab
echo "Checking the signature of $AAB"
"$JDK/jarsigner" -verify -certs "$AAB" | grep -E "jar verified|CN=" | head -3
ls -lh "$AAB"
echo "Done. Upload $AAB in Play Console."
