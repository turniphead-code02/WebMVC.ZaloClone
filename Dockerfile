# Stage 1: Build & Publish (.NET 10 SDK)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj và restore trước để tận dụng Docker layer cache
COPY src/ZaloClone/ZaloClone.csproj src/ZaloClone/
RUN dotnet restore src/ZaloClone/ZaloClone.csproj

# Copy toàn bộ source của app và publish Release
COPY src/ZaloClone/ src/ZaloClone/
WORKDIR /src/src/ZaloClone
RUN dotnet publish ZaloClone.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime (.NET 10 ASP.NET Core)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Tạo thư mục lưu file upload avatar và tệp chat
RUN mkdir -p wwwroot/uploads App_Data/uploads

# Render / Railway tự truyền biến môi trường PORT
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "dotnet ZaloClone.dll --urls http://0.0.0.0:${PORT:-8080}"]
