# ADR-0006 — a imagem é construída no pipeline (Azure DevOps), nunca na VPS:
# compilar .NET consome memória demais e competiria com o serviço em execução.

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia primeiro os arquivos de restore para aproveitar o cache de camada:
# mudança de código não invalida o restore de pacotes.
# O .editorconfig entra junto: ele carrega a supressão de análise nas migrations.
# Sem ele o publish falha aqui e passa na máquina do desenvolvedor.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/GrelhaHub.Domain/*.csproj          src/GrelhaHub.Domain/
COPY src/GrelhaHub.Application/*.csproj     src/GrelhaHub.Application/
COPY src/GrelhaHub.Infrastructure/*.csproj  src/GrelhaHub.Infrastructure/
COPY src/GrelhaHub.Api/*.csproj             src/GrelhaHub.Api/
RUN dotnet restore src/GrelhaHub.Api/GrelhaHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/GrelhaHub.Api/GrelhaHub.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# RN-GER-005 — a operação ocorre em America/Sao_Paulo. A imagem aspnet não traz o
# banco de dados de fusos horários, e TimeZoneInfo.FindSystemTimeZoneById falharia.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata curl \
 && rm -rf /var/lib/apt/lists/*
ENV TZ=America/Sao_Paulo

# Nunca rodar como root (ADR-0006).
RUN useradd --uid 1001 --create-home --shell /usr/sbin/nologin grelhahub

# ⚠ ADR-0015 — a raiz das imagens, criada AQUI e com dono 1001.
#
# Em produção este caminho é um VOLUME. Ao montar um volume vazio sobre um caminho
# que existe na imagem, o Docker copia o conteúdo E o dono desse caminho; sobre um
# caminho que NÃO existe, ele cria o ponto de montagem como root:root — e o
# processo, que roda como 1001, não consegue criar a pasta da unidade dentro dele.
#
# O modo de falha é o pior possível: a API sobe, o healthcheck fica verde, o
# cardápio responde, e só o primeiro upload de foto quebra — com 500 genérico,
# porque UnauthorizedAccessException não é regra de negócio. Foi assim que apareceu
# em produção em 18/08/2026.
#
# Volume que JÁ existe não é corrigido por esta linha: ele guarda o dono de quando
# nasceu. Ver "Imagens não gravam" em infra/README.md.
RUN mkdir -p /app/arquivos && chown 1001:1001 /app/arquivos

USER 1001

COPY --from=build --chown=1001:1001 /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=20s --retries=3 \
  CMD curl --fail --silent http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "GrelhaHub.Api.dll"]
