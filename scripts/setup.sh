#!/usr/bin/env bash
set -u

SKIP_ANDROID=0
SKIP_DESKTOP=0
for arg in "$@"; do
    case "$arg" in
        --skip-android) SKIP_ANDROID=1 ;;
        --skip-desktop) SKIP_DESKTOP=1 ;;
        -h|--help)
            echo "Usage: $0 [--skip-android] [--skip-desktop]"
            exit 0
            ;;
        *) echo "Unknown flag: $arg" >&2; exit 2 ;;
    esac
done

if [[ -t 1 ]]; then
    C_RESET=$'\033[0m'
    C_OK=$'\033[32m'
    C_FAIL=$'\033[31m'
    C_WARN=$'\033[33m'
    C_DIM=$'\033[2m'
    C_BOLD=$'\033[1m'
else
    C_RESET=""; C_OK=""; C_FAIL=""; C_WARN=""; C_DIM=""; C_BOLD=""
fi

FAILS=0
WARNS=0
OKS=0
declare -a HINTS=()

ok()   { printf "[ %sOK%s ]   %-20s%s\n" "$C_OK" "$C_RESET" "$1" "${2:-}"; OKS=$((OKS+1)); }
fail() { printf "[%sFAIL%s]   %-20s%s\n" "$C_FAIL" "$C_RESET" "$1" "${2:-not found}"; FAILS=$((FAILS+1));
         shift 2 || true
         for line in "$@"; do printf "         %s%s%s\n" "$C_DIM" "$line" "$C_RESET"; done; }
warn() { printf "[%sWARN%s]   %-20s%s\n" "$C_WARN" "$C_RESET" "$1" "${2:-}"; WARNS=$((WARNS+1));
         shift 2 || true
         for line in "$@"; do printf "         %s%s%s\n" "$C_DIM" "$line" "$C_RESET"; done; }

detect_distro() {
    local id="" id_like=""
    if [[ -r /etc/os-release ]]; then
        # shellcheck disable=SC1091
        . /etc/os-release
        id="${ID:-}"
        id_like="${ID_LIKE:-}"
    fi
    case " $id $id_like " in
        *" debian "*|*" ubuntu "*) echo "debian" ;;
        *" fedora "*|*" rhel "*|*" centos "*) echo "fedora" ;;
        *" arch "*) echo "arch" ;;
        *" suse "*|*" opensuse "*) echo "suse" ;;
        *) echo "unknown" ;;
    esac
}
DISTRO="$(detect_distro)"

pkg_hint() {
    local debian="$1" fedora="$2" arch="$3" suse="$4"
    case "$DISTRO" in
        debian) echo "Debian/Ubuntu: sudo apt install $debian" ;;
        fedora) echo "Fedora:        sudo dnf install $fedora" ;;
        arch)   echo "Arch:          sudo pacman -S $arch" ;;
        suse)   echo "openSUSE:      sudo zypper install $suse" ;;
        *)
            printf "Debian/Ubuntu: sudo apt install %s\n         Fedora:        sudo dnf install %s\n         Arch:          sudo pacman -S %s\n         openSUSE:      sudo zypper install %s" \
                "$debian" "$fedora" "$arch" "$suse"
            ;;
    esac
}

check_dotnet() {
    local sdks line v
    if ! command -v dotnet >/dev/null 2>&1; then
        fail ".NET SDK" "not found" "Install: https://dotnet.microsoft.com/download (need 10.0.300)"
        return
    fi
    sdks="$(dotnet --list-sdks 2>/dev/null || true)"
    while IFS= read -r line; do
        v="${line%% *}"
        if [[ "$v" == 10.0.* ]]; then
            ok ".NET SDK" "$v"
            return
        fi
    done <<< "$sdks"
    fail ".NET SDK" "no 10.0.x SDK found (need 10.0.300)" "Install: https://dotnet.microsoft.com/download"
}

check_node() {
    if ! command -v node >/dev/null 2>&1; then
        fail "Node.js" "not found" "Install: https://nodejs.org (need >=22)"
        return
    fi
    local v major
    v="$(node --version 2>/dev/null)"
    major="${v#v}"; major="${major%%.*}"
    if [[ "$major" =~ ^[0-9]+$ ]] && (( major >= 22 )); then
        ok "Node.js" "$v"
    else
        fail "Node.js" "$v (need >=22)" "Install: https://nodejs.org"
    fi
}

check_npm() {
    if ! command -v npm >/dev/null 2>&1; then
        fail "npm" "not found" "Comes with Node.js: https://nodejs.org"
        return
    fi
    ok "npm" "$(npm --version 2>/dev/null)"
}

check_rust() {
    if ! command -v rustc >/dev/null 2>&1 || ! command -v cargo >/dev/null 2>&1; then
        fail "Rust" "not found" "Install: https://rustup.rs   |   curl https://sh.rustup.rs -sSf | sh"
        return
    fi
    local rv cv
    rv="$(rustc --version 2>/dev/null | awk '{print $2}')"
    cv="$(cargo --version 2>/dev/null | awk '{print $2}')"
    ok "Rust" "rustc $rv / cargo $cv"
}

check_build_essentials() {
    local missing=()
    for tool in gcc pkg-config make; do
        command -v "$tool" >/dev/null 2>&1 || missing+=("$tool")
    done
    if (( ${#missing[@]} == 0 )); then
        ok "build tools" "gcc/pkg-config/make present"
    else
        fail "build tools" "missing: ${missing[*]}" \
            "$(pkg_hint 'build-essential pkg-config' 'gcc pkgconf-pkg-config make' 'base-devel' 'gcc pkg-config make')"
    fi
}

check_pkg() {
    local label="$1" pc="$2" debian="$3" fedora="$4" arch="$5" suse="$6"
    if ! command -v pkg-config >/dev/null 2>&1; then
        fail "$label" "pkg-config missing — cannot verify"
        return
    fi
    if pkg-config --exists "$pc" 2>/dev/null; then
        ok "$label"
        return
    fi
    fail "$label" "not found via pkg-config" "$(pkg_hint "$debian" "$fedora" "$arch" "$suse")"
}

check_appindicator() {
    if ! command -v pkg-config >/dev/null 2>&1; then
        fail "appindicator" "pkg-config missing — cannot verify"
        return
    fi
    # Distros disagree on the .pc name: ayatana-* on newer, plain appindicator3-0.1 on older.
    if pkg-config --exists "ayatana-appindicator3-0.1" 2>/dev/null \
       || pkg-config --exists "appindicator3-0.1" 2>/dev/null; then
        ok "appindicator"
        return
    fi
    fail "appindicator" "not found via pkg-config" \
        "$(pkg_hint 'libayatana-appindicator3-dev' 'libayatana-appindicator-gtk3-devel' 'libayatana-appindicator' 'libayatana-appindicator3-devel')"
}

check_tauri_libs() {
    check_pkg "webkit2gtk-4.1" "webkit2gtk-4.1" \
        "libwebkit2gtk-4.1-dev" "webkit2gtk4.1-devel" "webkit2gtk-4.1" "webkit2gtk3-devel"
    check_pkg "gtk+-3.0" "gtk+-3.0" \
        "libgtk-3-dev" "gtk3-devel" "gtk3" "gtk3-devel"
    check_pkg "librsvg-2.0" "librsvg-2.0" \
        "librsvg2-dev" "librsvg2-devel" "librsvg" "librsvg-devel"
    check_appindicator
    check_pkg "openssl" "openssl" \
        "libssl-dev" "openssl-devel" "openssl" "libopenssl-devel"
}

check_java() {
    if ! command -v java >/dev/null 2>&1; then
        fail "Java JDK" "not found (need 17+)" \
            "$(pkg_hint 'openjdk-17-jdk' 'java-17-openjdk-devel' 'jdk17-openjdk' 'java-17-openjdk-devel')"
        return
    fi
    local raw ver major
    raw="$(java -version 2>&1 | head -n1)"
    ver="$(printf '%s' "$raw" | sed -n 's/.*"\([0-9][0-9.]*\).*"/\1/p')"
    major="${ver%%.*}"
    if [[ "$major" == "1" ]]; then
        major="$(printf '%s' "$ver" | awk -F. '{print $2}')"
    fi
    if [[ "$major" =~ ^[0-9]+$ ]] && (( major >= 17 )); then
        ok "Java JDK" "$ver"
    else
        fail "Java JDK" "$ver (need 17+)" \
            "$(pkg_hint 'openjdk-17-jdk' 'java-17-openjdk-devel' 'jdk17-openjdk' 'java-17-openjdk-devel')"
    fi
}

check_android_sdk() {
    local home="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
    if [[ -z "$home" ]]; then
        fail "Android SDK" "ANDROID_HOME / ANDROID_SDK_ROOT not set" \
            "Install cmdline-tools and export ANDROID_HOME=\$HOME/Android/Sdk"
        return 1
    fi
    if [[ ! -x "$home/platform-tools/adb" ]]; then
        fail "Android SDK" "platform-tools/adb missing under $home" \
            "Run: sdkmanager 'platform-tools' 'platforms;android-34' 'build-tools;34.0.0'"
        return 1
    fi
    ok "Android SDK" "$home"
    printf '%s' "$home"
    return 0
}

check_sdkmanager() {
    local home="$1"
    if [[ -z "$home" ]]; then
        return
    fi
    for candidate in "$home/cmdline-tools/latest/bin/sdkmanager" "$home/cmdline-tools/bin/sdkmanager"; do
        if [[ -x "$candidate" ]]; then
            ok "sdkmanager" "$candidate"
            return
        fi
    done
    fail "sdkmanager" "cmdline-tools/latest/bin/sdkmanager not found" \
        "Download: https://developer.android.com/studio#command-tools" \
        "Extract to: \$ANDROID_HOME/cmdline-tools/latest/"
}

check_gradle_wrapper() {
    local gw="src/rAspCoreVueLauncher.Web/android/gradlew"
    if [[ -e "$gw" ]]; then
        if [[ -x "$gw" ]]; then
            ok "Gradle wrapper" "$gw"
        else
            warn "Gradle wrapper" "$gw exists but is not executable" "Run: chmod +x $gw"
        fi
    fi
}

printf "%s== rAspCoreVueLauncher setup check (Linux) ==%s\n\n" "$C_BOLD" "$C_RESET"

check_dotnet
check_node
check_npm

if (( SKIP_DESKTOP == 0 )); then
    check_rust
    check_build_essentials
    check_tauri_libs
fi

ANDROID_HOME_DETECTED=""
if (( SKIP_ANDROID == 0 )); then
    check_java
    ANDROID_HOME_DETECTED="$(check_android_sdk || true)"
    check_sdkmanager "$ANDROID_HOME_DETECTED"
fi

check_gradle_wrapper

printf "\nSummary: %s%d failed%s, %s%d warnings%s, %s%d ok%s.\n" \
    "$C_FAIL" "$FAILS" "$C_RESET" \
    "$C_WARN" "$WARNS" "$C_RESET" \
    "$C_OK"   "$OKS"   "$C_RESET"

if (( FAILS > 0 )); then
    cat <<EOF

Next steps:
  1. Install missing tools above.
  2. Re-run: ./scripts/setup.sh
  3. Then bootstrap deps:  dotnet restore  &&  (cd src/rAspCoreVueLauncher.Web && npm install)
EOF
    exit 1
fi

cat <<EOF

All checks passed. Bootstrap deps:
  dotnet restore  &&  (cd src/rAspCoreVueLauncher.Web && npm install)
EOF
exit 0
