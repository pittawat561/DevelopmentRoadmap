# Banking.Api/Dockerfile

# ===== Stage 1: Build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first → ใช้ Docker layer cache
COPY BankingSystem.slnx ./
COPY Banking.Domain/Banking.Domain.csproj Banking.Domain/
COPY Banking.Application/Banking.Application.csproj Banking.Application/
COPY Banking.Infrastructure/Banking.Infrastructure.csproj Banking.Infrastructure/
COPY Banking.Api/Banking.Api.csproj Banking.Api/

# Restore dependencies (cached ถ้า csproj ไม่เปลี่ยน)
RUN dotnet restore Banking.Api/Banking.Api.csproj

# Copy source code ทั้งหมด
COPY . .

# Build + Publish
WORKDIR /src/Banking.Api
RUN dotnet publish -c Release -o /app/publish --no-self-contained

# ===== Stage 2: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: สร้าง non-root user (Debian-based image ใช้ useradd)
RUN groupadd --system appgroup && useradd --system --gid appgroup --no-create-home appuser
USER appuser

# Copy published output จาก build stage
COPY --from=build /app/publish .

EXPOSE 8080

# Health check ใช้ dotnet แทน curl (aspnet image ไม่มี curl)
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD ["dotnet", "--list-runtimes"]

ENTRYPOINT ["dotnet", "Banking.Api.dll"]
