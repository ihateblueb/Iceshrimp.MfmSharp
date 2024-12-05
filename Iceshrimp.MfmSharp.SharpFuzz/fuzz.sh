#!/bin/bash
set -euo pipefail

which sharpfuzz 2>&1 >/dev/null
if [[ $? -ne 0 ]]; then
	dotnet tool install --global SharpFuzz.CommandLine
fi

input_dir="inputs"
output_dir="bin/fuzz"
findings_dir="findings"
project="Iceshrimp.MfmSharp.SharpFuzz"

mkdir -p "$output_dir"
mkdir -p "$findings_dir"

dotnet publish -c Release -o "$output_dir" -p:FUZZ=true
find "$output_dir" -name "*.dll" | grep -v "/$project.dll" | grep -v '/SharpFuzz.Common.dll' | grep -v '/SharpFuzz.dll' | grep -v '/Jetbrains.Annotations.dll' | xargs -n1 sharpfuzz 2>/dev/null || true

if [[ "$(uname)" == 'Darwin' ]]; then
  SL=/System/Library; PL=com.apple.ReportCrash
  launchctl unload -w ${SL}/LaunchAgents/${PL}.plist
  sudo launchctl unload -w ${SL}/LaunchDaemons/${PL}.Root.plist
fi

rm -rf "$findings_dir"

AFL_SKIP_BIN_CHECK=1
afl-fuzz -G 100000 -a text -t 1000 -i "inputs" -o "$findings_dir" -x mfm.dict "$(which dotnet)" "$output_dir/$project.dll"
