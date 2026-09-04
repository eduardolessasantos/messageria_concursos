FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Concurso.Web/Concurso.Web.csproj", "Concurso.Web/"]
COPY ["Concurso.Messaging/Concurso.Messaging.csproj", "Concurso.Messaging/"]

RUN dotnet restore "Concurso.Web/Concurso.Web.csproj"

COPY . .
WORKDIR "/src/Concurso.Web"
RUN dotnet publish "Concurso.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Concurso.Web.dll"]
