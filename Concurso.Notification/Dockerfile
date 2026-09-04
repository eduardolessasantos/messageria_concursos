FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Concurso.Notification/Concurso.Notification.csproj", "Concurso.Notification/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]
COPY ["Concurso.Shared/Concurso.Shared.csproj", "Concurso.Shared/"]

RUN dotnet restore "Concurso.Notification/Concurso.Notification.csproj"

COPY . .
WORKDIR "/src/Concurso.Notification"
RUN dotnet publish "Concurso.Notification.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Notification.dll"]
