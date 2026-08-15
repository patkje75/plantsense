# Publishes for the Raspberry Pi (framework-dependent — the Pi needs the ASP.NET Core 7 runtime).
# Use -rid linux-arm for 32-bit OS images.
param ([string]$rid = "linux-arm64")

dotnet publish PlantSense.csproj -c Release -r $rid --self-contained false
