#!/bin/bash
set -euo pipefail

set +e
which sharpfuzz 2>&1 >/dev/null
if [[ $? -ne 0 ]]; then
	dotnet tool install --global SharpFuzz.CommandLine
fi
set -e

if [[ $# -ne 3 ]]; then
  echo "Syntax: $0 <input> <mode> <identifier>"
  echo
  echo "Available inputs: combined, tests, manual"
  echo "Available modes: main, secondary"
  exit 1
fi

input_dir="inputs_$1"
output_dir="bin/fuzz"
findings_dir="findings_$1"
project="Iceshrimp.MfmSharp.SharpFuzz"

if [[ "$2" == "main" ]]; then
  mkdir -p "$output_dir"
  mkdir -p "$findings_dir"
  
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
  
  rm -rf "$findings_dir"
fi

export AFL_SKIP_BIN_CHECK=1

if [[ "$2" == 'main' ]]; then
  export AFL_FINAL_SYNC=1
  afl-fuzz -M "$3" -G 100000 -a text -t 1000 -i "$input_dir" -o "$findings_dir" -x mfm.dict "$(which dotnet)" "$output_dir/$project.dll"
elif [[ "$2" == 'secondary' ]]; then
  afl-fuzz -S "$3" -G 100000 -a text -t 1000 -i "$input_dir" -o "$findings_dir" -x mfm.dict "$(which dotnet)" "$output_dir/$project.dll"
fi
