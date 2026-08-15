# Copies the published build to the Raspberry Pi over SSH.
# Requires the OpenSSH client (ssh/scp), included with Windows 10+.
# Usage: .\deploy-ubuntu.ps1 -ip 192.168.1.50 -username ubuntu -destination /home/ubuntu/plantsense
param ([string]$ip, [string]$destination, [string]$username, [string]$rid = "linux-arm64")

scp -r ".\bin\Release\net7.0\$rid\publish\*" "${username}@${ip}:${destination}"

ssh "${username}@${ip}" "chmod u+x,o+x ${destination}"
