#!/bin/bash
# Runs the WP07 Mission EditMode tests outside Unity (TECH_PLAN §10.1 compile gate + a reflection NUnit runner on Unity's
# bundled .NET 8). Engine-only calls are avoided by the Mission code under test; tests that need Unity's test runner are
# reported as SKIP. Usage (project root or anywhere):
#   Tools/mission/offline/run_offline_tests.sh [--system Mission] [--filter=NamePart] [-v]
# Output: PASS/FAIL per test and a summary; exit 0 when nothing failed.
set -e
P=$(cd "$(dirname "$0")/../../.." && pwd)
SYSTEM=Mission
ARGS=()
while [ $# -gt 0 ]; do
  case "$1" in
    --system) SYSTEM="$2"; shift 2;;
    *) ARGS+=("$1"); shift;;
  esac
done
U=/Applications/Unity/Hub/Editor/6000.6.6f1/Unity.app/Contents
D=$U/Resources/Scripting/DotNetSdk
UE=$U/Resources/Scripting/Managed/UnityEngine
UED=$U/Resources/Scripting/Managed
NUNITD=$U/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net472/unity-custom
REF=$(ls -d $D/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
CSC=$(ls $D/sdk/*/Roslyn/bincore/csc.dll | head -1)
W=$(mktemp -d /tmp/ls_offline_tests.XXXXXX)
trap 'rm -rf "$W"' EXIT
REFS=""
for f in $REF/*.dll; do REFS="$REFS -r:$f"; done
$D/dotnet $CSC -nologo -langversion:latest -nullable:disable -out:$W/Runner.dll -target:exe $REFS -r:$NUNITD/nunit.framework.dll -r:$UE/UnityEngine.CoreModule.dll "$P/Tools/mission/offline/Runner.cs" >/dev/null
cat > $W/Runner.runtimeconfig.json <<JSON
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" }, "rollForward": "LatestMajor" } }
JSON
(cd "$P" && python3 Tools/build/compile_check.py --system "$SYSTEM" --keep "$W/keep" -q)
TEST_DLL="$W/keep/editor/LongShadow.Tests.EditMode.${SYSTEM}.dll"
export LS_PROJECT_ROOT="$P"
cd "$W" && $D/dotnet $W/Runner.dll "$TEST_DLL" "$W/keep/editor" $UE $UED $NUNITD "${ARGS[@]}"
