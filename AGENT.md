# AGENT.md: Diretrizes e Arquitetura do TrashAll

Este documento reúne o contexto de arquitetura, decisões técnicas e padrões de desenvolvimento do **TrashAll**.

---

## 1. Visão Geral do Projeto

O **TrashAll** é um utilitário desktop para Windows feito para triagem e limpeza rápida de pastas volumosas (Downloads, capturas de tela, fotos, vídeos e instaladores).

### Características principais
- **Operação pelo teclado**: Navegue com `A`/`D` (ou setas) e descarte com `Delete`/`X`/`Backspace`.
- **Exclusão segura**: Nenhum arquivo é apagado em definitivo pelo app. Todo descarte vai para a Lixeira do Windows, com restauração imediata pelo atalho `Ctrl+Z`.
- **Exibição imediata**: Exibe o primeiro item encontrado em menos de 1 segundo enquanto continua indexando a pasta em segundo plano em lotes (`batching`).
- **Comparação de duplicatas**: Agrupa arquivos pelo tamanho exato e valida a igualdade calculando hash SHA-256 dos blocos inicial e final.
- **Leitura direta de compactados**: Lista arquivos dentro de pacotes `.zip`, `.jar` e `.apk` sem extrair nada para o disco.

---

## 2. Stack Tecnológico

| Componente | Tecnologia | Detalhes / Versão |
|---|---|---|
| **Plataforma** | .NET 8.0 Windows | `net8.0-windows`, C# 12 |
| **Framework de UI** | WPF (Windows Presentation Foundation) | Estilizado com `WPF-UI` (Mica backdrop, tema dark) |
| **Padrão Arquitetural** | MVVM | `CommunityToolkit.Mvvm` (8.4.2) |
| **Processamento de Imagens** | Magick.NET | `Magick.NET-Q8-AnyCPU` (15.0.0) para suporte a `.psd`, `.psb`, `.svg` |
| **Visualização de PDFs** | Docnet.Core | `Docnet.Core` (2.6.0) para renderização da 1ª página em alta resolução |
| **Operações de Sistema** | Windows Shell / COM | `Microsoft.VisualBasic.FileIO.FileSystem` + COM `Shell.Application` |
| **Build & Release** | GitHub Actions | Single-file executável portátil x64 comprimido |

---

## 3. Estrutura do Código-Fonte

```
TrashAll/
├── .github/
│   └── workflows/
│       └── release.yml          # Pipeline de build e release automatizado
├── Helpers/
│   ├── Converters.cs            # Conversores XAML (BooleanToVis, NullToVis, etc.)
│   └── IconHelper.cs            # Extração de ícones de executáveis e sistema
├── Models/
│   ├── MediaItem.cs             # Modelo principal de cada arquivo indexado
│   ├── MediaType.cs             # Enum com as categorias de mídia suportadas
│   ├── UndoItem.cs              # Registro de histórico para desfazer exclusões (Ctrl+Z)
│   └── ZipPreviewData.cs        # Estrutura de pré-visualização de arquivos ZIP
├── Services/
│   ├── DuplicateDetectorService.cs # Análise e agrupamento de duplicatas via hash
│   ├── MediaLoaderService.cs    # Carregador assíncrono de bitmaps, vídeos e documentos
│   ├── MediaScannerService.cs   # Scanner reativo de diretórios com batch streaming
│   ├── ScreenshotGeneratorService.cs # Geração automatizada de capturas reais do app
│   ├── TrashService.cs          # Interação com Lixeira do Windows e restauração COM
│   └── ZipInspectorService.cs   # Leitura de manifestos de arquivos compactados
├── ViewModels/
│   └── MainViewModel.cs         # ViewModel central com comandos e propriedades reativas
├── App.xaml / App.xaml.cs       # Ponto de entrada da aplicação
├── MainWindow.xaml / .cs        # Janela principal com tema Fluent Dark
├── TrashAll.csproj              # Configurações do compilador e pacotes NuGet
├── Lancar-Release.bat           # Script de conveniência para criar tag e disparar CI
├── README.md                    # Apresentação do projeto no GitHub
└── AGENT.md                     # Este documento
```

---

## 4. Regras de Arquitetura e Boas Práticas

Ao modificar este projeto, siga rigorosamente as diretrizes abaixo:

### 4.1. Threading e Responsividade da UI
- **Nunca bloqueie a thread de UI (`Dispatcher`)**: O TrashAll lida com pastas de dezenas de milhares de itens sem travar a interface.
- Operações de I/O de disco, cálculo de hash SHA-256 e decodificação de imagens devem sempre rodar em `Task.Run` com suporte a `CancellationToken`.
- No carregamento de pastas (`MediaScannerService`), os itens são despachados em lotes periódicos (batching) para manter a interface fluida.

### 4.2. Segurança e Não Destruição de Dados
- **Regra de exclusão**: Nunca execute `File.Delete()` permanente no fluxo do usuário.
- Use exclusivamente o método `TrashService.MoveToRecycleBinAsync()`, que delega para a API de envio à Lixeira do Windows.
- O histórico de exclusões deve ser mantido em `UndoStack` para que o usuário possa pressionar `Ctrl+Z` e recuperar o arquivo sem perdas.

### 4.3. Interface e Estética
- A paleta visual é escura (Deep Dark / Mica), inspirada no design Fluent do Windows 11 com acentos em tons ciano/azul (`#38BDF8`), verde de sucesso (`#3FB950`), roxo de vídeos (`#A855F7`) e rosa para duplicatas (`#F472B6`).
- Mantenha espaçamentos confortáveis, cantos arredondados (`CornerRadius="6"` a `"10"`), microinterações de hover e tipografia legível.

### 4.4. Tratamento de Arquivos Inválidos ou Corrompidos
- Formatos proprietários ou arquivos corrompidos não devem quebrar o scanner nem a navegação.
- Se uma imagem ou vídeo falhar ao carregar, exiba o ícone nativo do sistema e uma mensagem de falha elegante sem lançar exceções não tratadas.

---

## 5. Fluxos de Execução e Comandos Úteis

### Executar localmente em desenvolvimento:
```powershell
dotnet run
```

### Compilar e validar:
```powershell
dotnet build -c Release
```

### Gerar binário único portátil (Self-Contained):
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./dist
```

### Lançar nova versão / release:
Execute o script interativo:
```cmd
.\Lancar-Release.bat
```
Ou crie e envie uma tag git:
```powershell
git tag -a v1.0.1 -m "TrashAll v1.0.1"
git push origin v1.0.1
```

---

## 6. Checklist de Validação
 
Antes de finalizar qualquer alteração neste repositório:
1. [ ] O código compila sem erros via `dotnet build`?
2. [ ] Todas as dependências externas necessárias foram adicionadas ao `.csproj`?
3. [ ] A exclusão segura para a Lixeira continua intacta?
4. [ ] O atalho `Ctrl+Z` restaura os arquivos corretamente?
5. [ ] O código permanece limpo, sem comentários desnecessários?
