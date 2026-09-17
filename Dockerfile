# Stage 1: build the React frontend
FROM node:24-alpine AS web-build
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# Stage 2: build the ASP.NET backend
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS server-build
WORKDIR /src/server
COPY server/server.csproj ./
RUN dotnet restore
COPY server/ ./
RUN dotnet publish -c Release --no-restore -o /app/publish

# Stage 3: the image that actually ships — runtime only, no build tools
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=server-build /app/publish ./
COPY --from=web-build /src/web/dist ./wwwroot
EXPOSE 8080
# Hosting platforms hand the port over in PORT; fall back to 8080 so local runs are unchanged.
ENTRYPOINT ["sh", "-c", "exec dotnet server.dll --urls http://+:${PORT:-8080}"]
