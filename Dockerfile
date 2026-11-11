# Imagem de produção do Bestia (ASP.NET Core 8). Variáveis esperadas em tempo de execução:
#   ConnectionStrings__Bestia, Seguranca__ChaveMestra, Seguranca__ChaveDeIndice,
#   Google__ClientId, Google__ClientSecret, Email__Servidor/Porta/Usuario/Senha/Remetente,
#   DataProtection__Pasta (volume persistente), Privacidade__EncarregadoEmail
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilar
WORKDIR /origem
COPY Directory.Build.props .
COPY src ./src
RUN dotnet publish src/Bestia.Web/Bestia.Web.csproj -c Release -o /publicado --nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=compilar /publicado .
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__Pasta=/chaves
VOLUME /chaves
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Bestia.Web.dll"]
