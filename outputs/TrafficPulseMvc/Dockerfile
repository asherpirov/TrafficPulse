FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT=TrafficWeb
WORKDIR /source
COPY . .
RUN dotnet publish ${PROJECT}/${PROJECT}.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
