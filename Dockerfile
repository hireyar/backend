FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /source
COPY . .
RUN dotnet restore HireYar.slnx && dotnet publish src/HireYar.Api -c Release --no-restore -o /out /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /keys /app/storage/interviews /app/upload-temp && chown $APP_UID:$APP_UID /keys /app/storage/interviews /app/upload-temp && chmod 700 /app/storage/interviews /app/upload-temp
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_TEMP=/app/upload-temp
EXPOSE 8080
ENTRYPOINT ["dotnet", "HireYar.Api.dll"]
