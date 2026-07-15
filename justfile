# rAspCoreVueLauncher — task runner
set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

# dev-api and dev-web need separate terminals (no reliable combined dev launcher for this repo yet)
dev-api:
    dotnet watch --project src/rAspCoreVueLauncher.Api/rAspCoreVueLauncher.Api.csproj run --launch-profile https

dev-web:
    npm --prefix src/rAspCoreVueLauncher.Web run dev

build:
    npm run build

test:
    npm run test
