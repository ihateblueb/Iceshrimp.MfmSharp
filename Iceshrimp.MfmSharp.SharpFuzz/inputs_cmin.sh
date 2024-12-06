#!/bin/bash
set -euo pipefail

set +e
which sharpfuzz 2>&1 >/dev/null
if [[ $? -ne 0 ]]; then
	dotnet tool install --global SharpFuzz.CommandLine
fi
set -e

input_dir="inputs"
output_dir="bin/fuzz"
project="Iceshrimp.MfmSharp.SharpFuzz"

dotnet publish -c Release -o "$output_dir" -p:FUZZ=true
find "$output_dir" -name "*.dll" | grep -v "/$project.dll" | grep -v '/SharpFuzz.Common.dll' | grep -v '/SharpFuzz.dll' | grep -v '/Jetbrains.Annotations.dll' | xargs -n1 sharpfuzz 2>/dev/null || true

if [[ "$(uname)" == 'Darwin' ]]; then
  SL=/System/Library; PL=com.apple.ReportCrash
  launchctl unload -w ${SL}/LaunchAgents/${PL}.plist
  sudo launchctl unload -w ${SL}/LaunchDaemons/${PL}.Root.plist
else
  echo 'core' | sudo tee /proc/sys/kernel/core_pattern
  echo 'performance' | sudo tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor
fi

export AFL_SKIP_BIN_CHECK=1
export AFL_MAP_SIZE=6989444 # workaround for https://github.com/AFLplusplus/AFLplusplus/issues/2223

rm -rf "${input_dir}_cmin"
afl-cmin -A -T all -i "$input_dir" -o "${input_dir}_cmin" -- "$(which dotnet)" "$output_dir/$project.dll"