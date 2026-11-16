# One image, three roles: `api`, `sweeper` and `relay` are chosen by the command and by
# Capacity__Roles. The same image runs the migrations (`... Api.dll migrate`) before they start.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ ./src/
RUN dotnet publish src/Presentation/Arvan.Capacity.Presentation.Api/Arvan.Capacity.Presentation.Api.csproj \
        -c Release -o /out/api /p:UseAppHost=false \
 && dotnet publish src/Presentation/Arvan.Capacity.Presentation.BackgroundService/Arvan.Capacity.Presentation.BackgroundService.csproj \
        -c Release -o /out/worker /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out/api ./api
COPY --from=build /out/worker ./worker
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
CMD ["dotnet", "/app/api/Arvan.Capacity.Presentation.Api.dll"]
