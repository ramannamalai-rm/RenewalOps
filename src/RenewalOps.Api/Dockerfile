FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Install Tesseract OCR and English language data
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    tesseract-ocr \
    tesseract-ocr-eng \
    libleptonica-dev \
    && rm -rf /var/lib/apt/lists/*

# Create tessdata directory and link system tessdata
RUN mkdir -p /app/tessdata && \
    cp /usr/share/tesseract-ocr/5/tessdata/eng.traineddata /app/tessdata/ 2>/dev/null || \
    cp /usr/share/tesseract-ocr/4.00/tessdata/eng.traineddata /app/tessdata/ 2>/dev/null || \
    true

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY nuget.config ./
COPY RenewalOps.sln ./
COPY src/RenewalOps.Api/RenewalOps.Api.csproj src/RenewalOps.Api/
COPY src/RenewalOps.Application/RenewalOps.Application.csproj src/RenewalOps.Application/
COPY src/RenewalOps.Domain/RenewalOps.Domain.csproj src/RenewalOps.Domain/
COPY src/RenewalOps.Infrastructure/RenewalOps.Infrastructure.csproj src/RenewalOps.Infrastructure/
COPY tests/RenewalOps.UnitTests/RenewalOps.UnitTests.csproj tests/RenewalOps.UnitTests/
COPY tests/RenewalOps.IntegrationTests/RenewalOps.IntegrationTests.csproj tests/RenewalOps.IntegrationTests/

RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish src/RenewalOps.Api/RenewalOps.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RenewalOps.Api.dll"]
