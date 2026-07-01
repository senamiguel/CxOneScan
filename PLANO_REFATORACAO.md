# Plano de Refatoração e Correção de Qualidade de Código (CxOneScan)

Este plano aborda a lista de problemas de UI/UX, Data Binding, Performance, Segurança, Code Smells e boas práticas de .NET no projeto.

## Principais Alterações Propostas

### 1. Segurança e Prevenção de Injeção de Argumentos
- **[ArgumentList no CLI](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CxCliService.cs):** Substituir a propriedade de string `StartInfo.Arguments` por `StartInfo.ArgumentList` em `CxCliService`. Isso elimina completamente qualquer risco de *Argument Injection* através de aspas em nomes de projetos ou tags.
- **[Zip Slip Validation](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CliInstallerService.cs):** Implementar verificação de path traversal durante a extração do zip da instalação do CLI, garantindo que nenhum arquivo seja extraído fora do diretório de destino.

### 2. UI/UX e Fluxo de Trabalho (Correções de Usabilidade)
- **Auto-save Assíncrono com Debounce:** Manter o comportamento conveniente de salvar automaticamente os detalhes do projeto, mas migrar a escrita em disco para uma thread secundária de forma assíncrona (`Task.Run`) com um **debounce de 300ms** (usando `CancellationTokenSource`) no evento de TextChanged. Isso evita qualquer travamento de digitação na UI thread.
- **Dois Sistemas de Seleção Resolvidos:**
  - Substituir o evento `SelectionChanged` da Grid de projetos por um evento de **Double-Click** (`MouseDoubleClick` / `DgProjects_MouseDoubleClick`).
  - Um clique único apenas seleciona a linha ou marca o checkbox (para scan). O painel lateral de edição de detalhes só é exibido ao dar um duplo clique na linha do projeto.
- **Melhoria de Nomes e Rótulos:**
  - Renomear o botão de importação para: **"📁 Escanear Pasta do Projeto"** (removendo a menção confusa a ".csproj" já que o fluxo importa diretórios).
- **Adições de Recursos de UX Faltantes:**
  - **Limpar Resultados:** Adicionar um botão **"🧹 Limpar Resultados"** na aba de Dashboard de Resultados para limpar a coleção `ScanResults` em memória.
  - **Confirmação ao Deletar:** Adicionar um diálogo de confirmação (`MessageBox.Show`) ao clicar em "🗑️" para remover projetos, evitando deleções acidentais.
  - **Adicionar com Enter:** Tratar o evento `KeyDown` no campo `txtNewProject` para adicionar o projeto automaticamente ao pressionar a tecla `Enter`.
  - **GridSplitter Visível:** Configurar o `GridSplitter` com cor visível (`Background="{DynamicResource CardStrokeColorDefaultBrush}"`) indicando claramente que o painel é redimensionável.
  - **Sincronização de ProgressBar e Status:** Ocultar a ProgressBar (`Visibility="Collapsed"`) no startup/ocioso e exibi-la apenas durante a execução de scans. Configurar o texto de status inicial para "⚠️ Não Configurado" ou "Pronto" alinhado com o estado real de autenticação e configurações.
  - **ToolTips:** Adicionar mensagens explicativas (ToolTips) em todos os botões de busca, comandos do CLI, ComboBox de Tema, e campos de entrada de dados.
  - **Console redimensionável:** Substituir `Height="120"` fixo do console por `RowDefinition Height="*"` com `MaxHeight="200"` e adicionar um `GridSplitter` próprio entre o conteúdo principal e o console, permitindo que o usuário redimensione a área verticalmente.
- **Tema Claro e Cores Semânticas:**
  - Substituir `Foreground="White"` por recursos de cores dinâmicas da biblioteca UI (`{DynamicResource TextFillColorPrimaryBrush}`) para garantir legibilidade tanto no tema Dark quanto no Light.
  - Definir cores de severidade dinâmicas (High, Medium, Low, Info) como recursos no XAML em vez de códigos hexadecimais estáticos.
  - Substituir o uso de `Brushes.Red`, `Brushes.Orange` e `Brushes.LightGreen` no code-behind por recursos estáticos ou variáveis de estado.

### 3. Integração e Configurações no Wizard
- **Configuração de URIs no Assistente:**
  - Adicionar campos avançados na Etapa 3 (Autenticação) do `SetupWizardWindow` para configuração de **Base URI** e **Base Auth URI** (pré-populados com os defaults).
  - Garantir que o assistente de configuração salve esses valores em `settings.json` ao concluir, evitando falsos positivos e permitindo ambientes customizados.
- **Reuso de HttpClient:**
  - Configurar a instalação e download do CLI para reutilizar a instância estática `HttpClient` compartilhada do `CheckmarxApiService`, evitando sobrecarga de sockets locais.

### 4. Remoção de Código Duplicado e Code Smells
- **[NEW] [CredentialService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CredentialService.cs):** Centralizar a criptografia/descriptografia da API Key (removendo duplicação entre `MainWindow` e `SetupWizardWindow`).
- **[NEW] [CliInstallerService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CliInstallerService.cs):** Centralizar a lógica de download, extração segura (Zip Slip free) e instalação do CLI de forma assíncrona.
- **[NEW] [AppConstants.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Common/AppConstants.cs):** Remover strings mágicas (URLs, paths, IDs de cliente) centralizando-as em constantes estáticas.
- **[NEW] [StringToVisibilityConverter.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Converters/StringToVisibilityConverter.cs):** Mover o conversor de visibilidade para um arquivo próprio e adicionar suporte correto a `FallbackValue`/`TargetNullValue`.
- **Tratamento de Exceções:** Corrigir blocos `catch` vazios para que registrem os erros no console em vez de engoli-los silenciosamente.
- **Decomposição de Métodos:** Dividir o método massivo `BtnScan_Click` em métodos auxiliares focados e legíveis.

### 5. Performance, Data Binding e Threading
- **Data Binding das Configurações:**
  - Migrar os binds do painel de Configurações para `{Binding Path=..., Source={x:Static services:AppSettingsService.Instance}}` no XAML, eliminando sincronizações manuais no code-behind.
  - Implementar `INotifyPropertyChanged` em `AppSettings` e `ScanResult` para propagação reativa de propriedades.
- **Cache da API Key em memória:** Armazenar a API Key descriptografada em um campo privado estático após a primeira leitura, evitando I/O + criptografia a cada `UpdateScanButtonState()`.
- **LINQ e I/O de Disco Otimizados:**
  - Em `ReportParserService.cs`, utilizar a classe `DirectoryInfo` para listar os arquivos de relatório, realizando o ordenamento em memória baseado na propriedade `LastWriteTimeUtc` já cacheada pelo sistema (evitando acessos O(N log N) repetidos ao disco).
- **LongRunning Tasks:**
  - Modificar a chamada do processo de scan em `CxCliService` para usar `Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)`, liberando a thread pool durante a longa execução do CLI.
- **Tratamento de async void:**
  - Envolver o corpo de **todos os event handlers `async void`** (`BtnSettingInstallCli_Click`, `BtnSettingAuth_Click`, `BtnScan_Click`, `BtnInstallCli_Click`, `BtnValidateAuth_Click`) em blocos `try-catch` que registrem o erro no console (`AppendToConsole`), evitando que exceções não tratadas derrubem o processo.

---

## Proposed Changes

### Common / Converters

#### [NEW] [AppConstants.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Common/AppConstants.cs)
#### [NEW] [StringToVisibilityConverter.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Converters/StringToVisibilityConverter.cs)

---

### Modelos

#### [MODIFY] [ScanResult.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Models/ScanResult.cs)
- Implementar `INotifyPropertyChanged`.

#### [MODIFY] [AppSettings.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Models/AppSettings.cs)
- Implementar `INotifyPropertyChanged`.

---

### Serviços

#### [NEW] [CredentialService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CredentialService.cs)
- Criptografia e descriptografia de credenciais.

#### [NEW] [CliInstallerService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CliInstallerService.cs)
- Instalação e download seguro do CLI utilizando o HttpClient estático compartilhado.

#### [MODIFY] [CheckmarxApiService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CheckmarxApiService.cs)
- Otimizar LINQ e remover operadores null-forgiving inseguros.

#### [MODIFY] [CxCliService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/CxCliService.cs)
- Migrar para `ArgumentList` e `LongRunning` Task.

#### [MODIFY] [ReportParserService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/ReportParserService.cs)
- Otimizar I/O de disco no carregamento de relatórios.

#### [DELETE] [SolutionParserService.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/Services/SolutionParserService.cs)
- Remover arquivo agora sem utilidade no projeto.

---

### UI (Interface Gráfica)

#### [MODIFY] [MainWindow.xaml](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/MainWindow.xaml)
- Ajustes de cores dinâmicas (substituir `Foreground="White"` por `{DynamicResource TextFillColorPrimaryBrush}`)
- Migrar TextBoxes/CheckBoxes do painel de Configurações para uso de Binding com `{x:Static services:AppSettingsService.Instance}`
- GridSplitter visível com `Background="{DynamicResource CardStrokeColorDefaultBrush}"`
- Console: mudar de `Height="120"` fixo para `RowDefinition Height="*"` com `MaxHeight="200"` e adicionar `GridSplitter` próprio
- WrapPanel nos tipos de scan (SAST/SCA/Incremental) para evitar clipping
- Adicionar botão "🧹 Limpar Resultados" na aba Dashboard
- Adicionar `TextTrimming` no nome do projeto e `TextWrapping` nas mensagens de status
- Aumentar touch target do botão "＋" (mínimo 44px) e do botão de lixeira
- Adicionar ToolTips em todos os botões, ComboBox de Tema e campos de entrada
- Esconder ProgressBar (`Visibility="Collapsed"`) no estado ocioso, mostrar apenas durante scan

#### [MODIFY] [MainWindow.xaml.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/MainWindow.xaml.cs)
- Implementar auto-save com debounce assíncrono (300ms) via `CancellationTokenSource` nos eventos `DetailField_TextChanged` e `DetailCheckbox_Changed`
- Substituir `DgProjects_SelectionChanged` por `DgProjects_MouseDoubleClick` para abrir detalhes
- Adicionar confirmação `MessageBox` em `BtnRemoveProject_Click`
- Adicionar `KeyDown` handler em `txtNewProject` para Enter submeter
- Delegar criptografia para `CredentialService` e instalação CLI para `CliInstallerService`
- Adicionar cache da API Key em campo privado (`_cachedApiKey`), atualizado ao salvar/carregar
- Envolver todos os `async void` event handlers em try-catch com `AppendToConsole` para log de erros
- Adicionar `using CxDesktopWrapper.Common;` para acesso às constantes

#### [MODIFY] [SetupWizardWindow.xaml](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/SetupWizardWindow.xaml)
- Adicionar campos de Base URI e Base Auth URI na Etapa 3 (Autenticação)
- Cores dinâmicas (substituir `Foreground="White"` por recursos do tema)
- Indicadores de passo ampliados (mínimo 12x12px)

#### [MODIFY] [SetupWizardWindow.xaml.cs](file:///c:/Users/MIGUEL.SENA/Documents/CxOneScan/SetupWizardWindow.xaml.cs)
- Salvar BaseUri e BaseAuthUri das configurações no `settings.json` ao concluir
- Delegar criptografia para `CredentialService` e instalação CLI para `CliInstallerService`
- Substituir comparação frágil `lblAuthStatus.Text != "Autenticado"` por variável booleana `_isAuthValidated`
- Envolver `async void` event handlers em try-catch
- Adicionar `using CxDesktopWrapper.Common;` para acesso às constantes

---

## Verification Plan

### Automated Verification
- Garantir compilação bem-sucedida da solução através do Visual Studio.

### Manual Verification
1.  Verificar o visual do app alternando entre os temas Light e Dark para garantir a legibilidade de todos os textos, ToolTips, status bar e GridSplitter.
2.  Testar a navegação por teclado no Wizard usando Tab e Alt+atalho.
3.  Simular uma autenticação com chave inválida para validar as mensagens de erro detalhadas.
4.  Inserir caracteres especiais (como aspas `"` e ponto e vírgula `;`) no nome do projeto local e disparar o scan para garantir que a proteção do `ArgumentList` previne qualquer injeção no CLI.
5.  Digitar caracteres especiais (aspas, barras, acentos) no nome do projeto via teclado e verificar salvamento com debounce sem travamentos.
6.  Executar scan com rede offline e verificar mensagem de erro amigável (sem crash).
7.  Fechar e reabrir o app e verificar que todos os projetos e configurações persistem corretamente.
8.  Testar double-click em projeto para abrir detalhes vs clique único para selecionar checkbox.
9.  Redimensionar o console via GridSplitter e verificar que a altura persiste dentro dos limites.
10. Clicar em "Limpar Resultados" e verificar que a DataGrid de resultados é esvaziada.
