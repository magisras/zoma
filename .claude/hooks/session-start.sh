#!/bin/bash
# Cloud sessions start from a fresh container with no C# compiler. tools/check.sh (and
# tools/headless.sh) need Mono's mcs and runtime, and the browser sandbox needs the .NET 8 SDK,
# so install them before the session begins. Ubuntu's packages: Microsoft's download host is
# blocked in cloud sessions, nuget.org is not.
# On a laptop this does nothing: install them yourself (brew install mono; brew install --cask dotnet-sdk).
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

export DEBIAN_FRONTEND=noninteractive
install_packages() {
  # Try without refreshing the package index first (fast); refresh only if that fails.
  apt-get install -y -qq "$@" >/dev/null 2>&1 || {
    apt-get update -qq
    apt-get install -y -qq "$@"
  }
}

# Already there (container restored from cache): nothing to do.
if ! command -v mcs >/dev/null 2>&1 || ! command -v mono >/dev/null 2>&1; then
  install_packages mono-mcs mono-runtime libmono-system-core4.0-cil
fi
if ! command -v dotnet >/dev/null 2>&1; then
  install_packages dotnet-sdk-8.0
fi

mcs --version
dotnet --version
