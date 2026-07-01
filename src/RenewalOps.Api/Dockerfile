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

# The Tesseract .NET wrapper ships Windows DLLs under x64/ and, on Linux, looks for
# matching .so names (libleptonica-1.82.0.so, libtesseract50.so). Symlink the system
# libraries (installed in the base stage) to those expected names so native interop resolves.
# Also provide libdl.so: Debian 12 merged libdl into libc, but the wrapper still dlopen's
# "libdl", so alias it to libdl.so.2.
RUN ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.82.0.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /app/x64/libtesseract50.so && \
    ln -sf /usr/lib/x86_64-linux-gnu/libdl.so.2 /usr/lib/x86_64-linux-gnu/libdl.so
ENV LD_LIBRARY_PATH=/app/x64:/usr/lib/x86_64-linux-gnu

ENTRYPOINT ["dotnet", "RenewalOps.Api.dll"]
