# Lives in .eng/docker/ - must be built with the repo root as context (its COPY instructions
# are written relative to that root, e.g. "COPY src/ src/"), e.g.
# `docker build -f .eng/docker/Dockerfile .` from the repo root, or via
# .eng/docker/docker-compose.yml (which sets context/dockerfile accordingly). .dockerignore
# stays at the repo root for the same reason - Docker only auto-discovers it at the build
# context's root, not next to a relocated Dockerfile.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src

COPY CodeCiir.slnx stylecop.json ./
COPY src/CodeCiir.Api/CodeCiir.Api.csproj src/CodeCiir.Api/
COPY src/CodeCiir.Application/CodeCiir.Application.csproj src/CodeCiir.Application/
COPY src/CodeCiir.Infrastructure.Database/CodeCiir.Infrastructure.Database.csproj src/CodeCiir.Infrastructure.Database/
COPY src/CodeCiir.Embeddings.Abstraction/CodeCiir.Embeddings.Abstraction.csproj src/CodeCiir.Embeddings.Abstraction/
COPY src/CodeCiir.Embeddings.Ollama/CodeCiir.Embeddings.Ollama.csproj src/CodeCiir.Embeddings.Ollama/
COPY src/CodeCiir.Reranking.Abstraction/CodeCiir.Reranking.Abstraction.csproj src/CodeCiir.Reranking.Abstraction/
COPY src/CodeCiir.Reranking.Ollama/CodeCiir.Reranking.Ollama.csproj src/CodeCiir.Reranking.Ollama/
COPY src/CodeCiir.Reranking.OpenAI/CodeCiir.Reranking.OpenAI.csproj src/CodeCiir.Reranking.OpenAI/
COPY src/CodeCiir.Mcp/CodeCiir.Mcp.csproj src/CodeCiir.Mcp/
RUN dotnet restore src/CodeCiir.Api/CodeCiir.Api.csproj

COPY src/ src/
RUN dotnet publish src/CodeCiir.Api/CodeCiir.Api.csproj -c Release -o /app -p:Version="$VERSION"

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN useradd --create-home appuser
USER appuser

COPY --from=build --chown=appuser:appuser /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CodeCiir.Api.dll"]
