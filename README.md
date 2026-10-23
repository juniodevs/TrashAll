# TrashAll

Triagem e limpeza rápida de arquivos de mídia para Windows.

## O que é

TrashAll é um aplicativo desktop para Windows voltado para quem acumula pastas pesadas de downloads, capturas, fotos, vídeos e arquivos de trabalho e precisa passar o pente fino sem perder tempo com o mouse.

A navegação foi pensada para uso direto no teclado: você abre uma pasta grande, visualiza o arquivo na hora e decide se mantém ou manda para a Lixeira do Windows.

## Formatos suportados

- Imagens: JPG, PNG, WEBP, GIF, BMP, TIFF, SVG, ICO e PSD/PSB (arquivos do Photoshop decodificados via ImageMagick).
- Documentos: PDFs (renderiza a primeira página em alta resolução para conferência rápida).
- Vídeos e Áudio: MP4, MKV, MOV, WEBM, AVI, WMV, MP3, WAV, FLAC.
- Pacotes compactados: ZIP, JAR, APK (com leitor interno para conferir o que tem dentro sem precisar descompactar).
- Executáveis e instaladores: EXE, MSI, BAT, CMD, ISO.

## Recursos principais

- Varredura instantânea: exibe o primeiro arquivo encontrado em menos de 1 segundo enquanto continua indexando o restante da pasta em segundo plano.
- Envio seguro para a Lixeira: os arquivos apagados vão para a Lixeira do Windows, sem exclusão permanente direta.
- Desfazer imediato (Ctrl+Z): se apagar um arquivo sem querer, o atalho recupera o arquivo direto da Lixeira e o coloca de volta na lista.
- Detecção de duplicados: localiza arquivos idênticos usando tamanho e verificação de hash SHA-256. Agrupa as cópias lado a lado e marca quem é original e quem é cópia.
- Filtros rápidos por tamanho: botões de um clique para isolar arquivos maiores que 1 GB, 100 MB ou 10 MB.
- Busca em tempo real: filtra por nome ou extensão instantaneamente conforme a digitação.
- Dois modos de visualização: visualizador focado em mídia ou tabela detalhada em modo lista.

## Atalhos de teclado

| Tecla | Ação |
|---|---|
| `D` ou `Seta Direita` | Próximo arquivo |
| `A` ou `Seta Esquerda` | Arquivo anterior |
| `Delete`, `X` ou `Backspace` | Mover para a Lixeira |
| `Ctrl + Z` | Desfazer última exclusão |
| `Espaço` | Reproduzir ou pausar vídeo |
| `F` ou `F11` | Alternar tela cheia |

## Como compilar e rodar

Requisitos:
- Windows 10 ou 11 (64-bit)
- .NET 8.0 SDK

Para executar em modo de desenvolvimento:
```bash
dotnet run
```

Para gerar a versão portátil única (arquivo único que não exige .NET instalado no computador de destino):
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./dist
```
