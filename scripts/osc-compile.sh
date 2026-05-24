#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tmp_dir="$(mktemp -d /tmp/penrose-osc-compile.XXXXXX)"
trap 'rm -rf "$tmp_dir"' EXIT

cat > "$tmp_dir/PenroseOscCompile.csproj" <<CSHARP_PROJECT
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$repo_root/Assets/OSC/**/*.cs" Exclude="$repo_root/Assets/OSC/Tests/**/*.cs" />
  </ItemGroup>
</Project>
CSHARP_PROJECT

dotnet build "$tmp_dir/PenroseOscCompile.csproj" -v:minimal
