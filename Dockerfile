# syntax=docker/dockerfile:1

# 1. 優化建置效能：確保 SDK 在原生架構下執行
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. 接收從 podman build 傳入的目標架構
ARG TARGETARCH

# (優化建議) 先複製專案檔並還原，可以利用快取層
COPY Oracle-MCP/Oracle-MCP.csproj Oracle-MCP/
RUN dotnet restore Oracle-MCP/Oracle-MCP.csproj

COPY . .

# 3. 根據 TARGETARCH 動態決定 .NET 的 RID
RUN case ${TARGETARCH} in \
         "amd64") TARGET_RID="linux-x64" ;; \
         "arm64") TARGET_RID="linux-arm64" ;; \
    esac && \
    dotnet publish Oracle-MCP/Oracle-MCP.csproj -c Release -o /app/publish -r ${TARGET_RID} --no-self-contained false

# 由於我們使用了 -r 參數，所以這是一個自封閉應用程式。
# 因此，執行階段映像檔只需要一個基礎映像即可，不一定需要 aspnet 執行環境。
# 但為了保持與您原始設定的一致性，我們仍然使用 aspnet:8.0。
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
# 自封閉應用程式不需要 DOTNET_EnableDiagnostics
# ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish /app
RUN ls -al /app
ENTRYPOINT ["/app/Oracle-MCP"]
