using Microsoft.Win32;
using Advice = CS2Optimizer.Core.Oosu10.Oosu10Advice;
using static CS2Optimizer.Core.Oosu10.Oosu10Groups;

namespace CS2Optimizer.Core.Oosu10;

/// <summary>
/// Catálogo dos ajustes do O&amp;O ShutUp10 (arquivo OOSU10.reg) traduzidos para PT-BR.
/// Cada item guarda o estado "ligado" (<see cref="Oosu10Tweak.Enable"/>) e, quando o
/// arquivo traz o estado alternativo, o estado de volta (<see cref="Oosu10Tweak.Restore"/>).
/// </summary>
public static class Oosu10Catalog
{
    private const string Cu = @"HKCU\";
    private const string Lm = @"HKLM\";

    private const string LegacyEdge =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppContainer\Storage\microsoft.microsoftedge_8wekyb3d8bbwe\MicrosoftEdge";

    private const string Consent =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private const string Cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    public static IReadOnlyList<Oosu10Tweak> All { get; } = Build();

    private static IReadOnlyList<Oosu10Tweak> Build()
    {
        var list = new List<Oosu10Tweak>();

        // ---------------------------------------------------------------- Privacidade
        list.Add(T("adv-id", Privacidade,
            "Desativar e redefinir o ID de publicidade",
            "Zera o ID de publicidade e desliga anúncios personalizados nos apps da Store.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 1),
            explanation: "O Windows cria um ID por usuário para personalizar anúncios em apps da Microsoft Store. " +
                         "Zerar o valor desliga essa personalização; não afeta o CS2 nem a Steam."));

        list.Add(T("typing-tipc", Privacidade,
            "Desativar o envio de dados de digitação",
            "Impede o envio de amostras de texto digitado para a Microsoft (TIPC).",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Input\TIPC", "Enabled", 0)));

        list.Add(T("timeline-suggestions", Privacidade,
            "Desativar sugestões na Linha do Tempo",
            "Remove as sugestões de conteúdo do histórico de atividades.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SubscribedContent-353698Enabled", 0)));

        list.Add(T("start-suggestions", Privacidade,
            "Desativar sugestões no menu Iniciar",
            "Remove os blocos e sugestões de apps patrocinados no Iniciar.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SubscribedContent-338388Enabled", 0)));

        list.Add(T("tips-suggestions", Privacidade,
            "Desativar dicas e truques do Windows",
            "Desliga as notificações de dicas, truques e sugestões do sistema.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SubscribedContent-338389Enabled", 0)));

        list.Add(T("settings-suggestions", Privacidade,
            "Desativar conteúdo sugerido no app Configurações",
            "Remove os cartões de sugestão dentro das Configurações do Windows.",
            Advice.Recomendado, true,
            Merge(BothDw(Cdm, "SubscribedContent-338393Enabled", 0),
                BothDw(Cdm, "SubscribedContent-353694Enabled", 0),
                BothDw(Cdm, "SubscribedContent-353696Enabled", 0))));

        list.Add(T("setup-suggestions", Privacidade,
            "Desativar a sugestão de concluir a configuração do dispositivo",
            "Impede o Windows de sugerir finalizar o setup do PC (SCOOBE).",
            Advice.Recomendado, true,
            Merge(BothDw(Cdm, "SubscribedContent-310093Enabled", 0),
                BothDw(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", 0))));

        list.Add(T("app-notifications", Privacidade,
            "Desativar notificações de apps",
            "Bloqueia os balões de notificação de todos os apps.",
            Advice.Opcional, false,
            Merge(BothDw(@"Software\Policies\Microsoft\Windows\CurrentVersion\PushNotifications", "NoToastApplicationNotification", 1),
                BothDw(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0)),
            note: "Não recomendado: você perde avisos de apps, inclusive de mensagens e atualizações."));

        list.Add(T("browser-language", Privacidade,
            "Não expor o idioma do Windows aos sites",
            "Marca HttpAcceptLanguageOptOut = 1 para os navegadores não enviarem o idioma local.",
            Advice.Opcional, false,
            BothDw(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1),
            note: "Não recomendado: alguns sites deixam de abrir já no seu idioma."));

        list.Add(T("touch-text-prediction", Privacidade,
            "Desativar sugestões de texto do teclado virtual",
            "Desliga a predição de texto do teclado de toque.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\TabletTip\1.7", "EnableTextPrediction", 0)));

        list.Add(T("store-urls", Privacidade,
            "Não enviar URLs de apps para a Store",
            "Impede que apps abram a Store (ms-windows-store://) para avaliar links.",
            Advice.Opcional, false,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\AppHost", "EnableWebContentEvaluation", 0)));

        list.Add(T("handwriting-sharing", Privacidade,
            "Desativar compartilhamento de dados de escrita à mão",
            "Bloqueia o envio de dados de handwriting para a Microsoft.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\TabletPC", "PreventHandwritingDataSharing", 1)));

        list.Add(T("handwriting-errors", Privacidade,
            "Desativar envio de erros de escrita à mão",
            "Impede o envio de relatórios de erro de reconhecimento de escrita.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\HandwritingErrorReports", "PreventHandwritingErrorReports", 1)));

        list.Add(T("inventory-collector", Privacidade,
            "Desativar o Coletor de Inventário (AppCompat)",
            "Desliga o inventário de aplicativos usado pela telemetria de compatibilidade.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\AppCompat", "DisableInventory", 1)));

        list.Add(T("lockscreen-camera", Privacidade,
            "Desativar a câmera na tela de bloqueio",
            "Impede o uso da câmera a partir da tela de bloqueio.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Personalization", "NoLockScreenCamera", 1)));

        list.Add(T("bluetooth-ads", Privacidade,
            "Desativar anúncios por Bluetooth",
            "Impede que apps anunciem serviços via Bluetooth.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\PolicyManager\current\device\Bluetooth", "AllowAdvertising", 0)));

        list.Add(T("ceip", Privacidade,
            "Desativar o CEIP (Programa de Experiência do Usuário)",
            "Sai do programa de melhoria contínua da Microsoft.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\SQMClient\Windows", "CEIPEnable", 0),
            BothDw(@"Software\Microsoft\SQMClient\Windows", "CEIPEnable", 1)));

        list.Add(T("sms-cloud-backup", Privacidade,
            "Desativar backup de mensagens de texto na nuvem",
            "Grava AllowMessageSync = 0 no Windows.",
            Advice.Opcional, false,
            BothDw(@"Software\Policies\Microsoft\Windows\Messaging", "AllowMessageSync", 0),
            BothDw(@"Software\Policies\Microsoft\Windows\Messaging", "AllowMessageSync", 1)));

        list.Add(T("wer", Privacidade,
            "Desativar o Windows Error Reporting (WER)",
            "Para de empacotar e enviar relatórios de erro para a Microsoft.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\Windows Error Reporting", "Disabled", 1),
            note: "O Windows continua mostrando o erro na tela; só deixa de enviar o relatório."));

        list.Add(T("biometrics", Privacidade,
            "Desativar os recursos biométricos (Windows Hello)",
            "Desliga o Windows Hello (rosto/impressão digital) por política.",
            Advice.Opcional, false,
            BothDw(@"Software\Policies\Microsoft\Biometrics", "Enabled", 0),
            BothDw(@"Software\Policies\Microsoft\Biometrics", "Enabled", 1),
            note: "Não recomendado: você perde o login por rosto/impressão digital."));

        // ------------------------------------------- Histórico e Área de Transferência
        list.Add(T("clipboard-history", Historico,
            "Desativar o histórico da área de transferência",
            "Desliga o Win+V e o histórico de itens copiados.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Clipboard", "EnableClipboardHistory", 0),
            explanation: "O copiar/colar normal continua funcionando; só o histórico (Win+V) e a lista de itens recentes são desligados."));

        list.Add(T("clipboard-machine", Historico,
            "Bloquear o histórico de clipboard para a máquina toda",
            "Política AllowClipboardHistory = 0 para todos os usuários.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\System", "AllowClipboardHistory", 0)));

        list.Add(T("activity-feed", Historico,
            "Desativar gravação da atividade do usuário",
            "Desliga o feed de atividades (Timeline).",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0)));

        list.Add(T("publish-activities", Historico,
            "Parar de armazenar o histórico de atividades",
            "Grava PublishUserActivities = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\System", "PublishUserActivities", 0)));

        list.Add(T("upload-activities", Historico,
            "Parar de enviar atividades para a Microsoft",
            "Grava UploadUserActivities = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\System", "UploadUserActivities", 0)));

        list.Add(T("cross-device-clipboard", Historico,
            "Parar de sincronizar o clipboard entre dispositivos",
            "Grava AllowCrossDeviceClipboard = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard", 0)));

        // ------------------------------------------------------------ Permissões de Apps
        list.Add(T("cap-account-info", Permissoes,
            "Bloquear acesso de apps aos dados da conta",
            "ConsentStore de userAccountInformation = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\userAccountInformation", "Value", "Deny"),
            BothSt(Consent + @"\userAccountInformation", "Value", "Allow")));

        list.Add(T("app-start-tracking", Permissoes,
            "Desativar o rastreio de apps iniciados",
            "Start_TrackProgs = 0: o Iniciar para de registrar o que você abre.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0)));

        list.Add(T("cap-diagnostics", Permissoes,
            "Bloquear acesso de apps a dados de diagnóstico",
            "ConsentStore de appDiagnostics = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\appDiagnostics", "Value", "Deny"),
            BothSt(Consent + @"\appDiagnostics", "Value", "Allow")));

        list.Add(T("cap-location", Permissoes,
            "Bloquear acesso de apps à localização",
            "Negar location e location\\NonPackaged (apps da Store e programas comuns).",
            Advice.Recomendado, true,
            Merge(BothSt(Consent + @"\location", "Value", "Deny"),
                BothSt(Consent + @"\location\NonPackaged", "Value", "Deny")),
            Merge(BothSt(Consent + @"\location", "Value", "Allow"),
                BothSt(Consent + @"\location\NonPackaged", "Value", "Allow"))));

        list.Add(T("cap-camera", Permissoes,
            "Bloquear acesso de apps à câmera",
            "ConsentStore de webcam = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\webcam", "Value", "Deny"),
            BothSt(Consent + @"\webcam", "Value", "Allow"),
            note: "Não recomendado: quebra webcam em apps da Store, reuniões e reconhecimento facial."));

        list.Add(T("cap-microphone", Permissoes,
            "Bloquear acesso de apps ao microfone",
            "ConsentStore de microphone = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\microphone", "Value", "Deny"),
            BothSt(Consent + @"\microphone", "Value", "Allow"),
            note: "Não recomendado: quebra microfone em apps da Store, Discord da Store e chamadas."));

        list.Add(T("cap-voice-activation", Permissoes,
            "Desativar ativação por voz para todos os apps",
            "AgentActivationEnabled = 0 (só vale se a fala estiver ativa).",
            Advice.Opcional, false,
            BothDw(@"Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps", "AgentActivationEnabled", 0),
            BothDw(@"Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps", "AgentActivationEnabled", 1)));

        list.Add(T("cap-voice-activation-lock", Permissoes,
            "Desativar ativação por voz com o PC bloqueado",
            "AgentActivationOnLockScreenEnabled = 0.",
            Advice.Opcional, false,
            BothDw(@"Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps", "AgentActivationOnLockScreenEnabled", 0)));

        list.Add(T("headset-button", Permissoes,
            "Desativar o app padrão do botão do headset",
            "AgentActivationLastUsed = 0: o botão do headset para de reabrir o último app.",
            Advice.Opcional, false,
            BothDw(@"Software\Microsoft\Speech_OneCore\Settings\VoiceActivation\UserPreferenceForAllApps", "AgentActivationLastUsed", 0),
            note: "O Windows protege essa chave: nem administrador consegue gravar. Fica fora das recomendadas."));

        list.Add(T("cap-notifications", Permissoes,
            "Bloquear acesso de apps às suas notificações",
            "ConsentStore de userNotificationListener = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\userNotificationListener", "Value", "Deny"),
            BothSt(Consent + @"\userNotificationListener", "Value", "Allow")));

        list.Add(T("cap-activity-sensors", Permissoes,
            "Bloquear acesso de apps aos sensores de movimento",
            "ConsentStore de activity = Deny (giroscópio/rotação).",
            Advice.Opcional, false,
            BothSt(Consent + @"\activity", "Value", "Deny"),
            BothSt(Consent + @"\activity", "Value", "Allow")));

        list.Add(T("cap-contacts", Permissoes,
            "Bloquear acesso de apps a contatos e calendário",
            "Negar contacts e appointments.",
            Advice.Recomendado, true,
            Merge(BothSt(Consent + @"\contacts", "Value", "Deny"),
                BothSt(Consent + @"\appointments", "Value", "Deny")),
            Merge(BothSt(Consent + @"\contacts", "Value", "Allow"),
                BothSt(Consent + @"\appointments", "Value", "Allow"))));

        list.Add(T("cap-phone-calls", Permissoes,
            "Bloquear acesso de apps a chamadas",
            "ConsentStore de phoneCall = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\phoneCall", "Value", "Deny"),
            BothSt(Consent + @"\phoneCall", "Value", "Allow")));

        list.Add(T("cap-call-history", Permissoes,
            "Bloquear acesso de apps ao histórico de chamadas",
            "ConsentStore de phoneCallHistory = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\phoneCallHistory", "Value", "Deny"),
            BothSt(Consent + @"\phoneCallHistory", "Value", "Allow")));

        list.Add(T("cap-email", Permissoes,
            "Bloquear acesso de apps a e-mail",
            "ConsentStore de email = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\email", "Value", "Deny"),
            BothSt(Consent + @"\email", "Value", "Allow")));

        list.Add(T("cap-tasks", Permissoes,
            "Bloquear acesso de apps à lista de tarefas",
            "ConsentStore de userDataTasks = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\userDataTasks", "Value", "Deny"),
            BothSt(Consent + @"\userDataTasks", "Value", "Allow")));

        list.Add(T("cap-messages", Permissoes,
            "Bloquear acesso de apps a mensagens",
            "ConsentStore de chat = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\chat", "Value", "Deny"),
            BothSt(Consent + @"\chat", "Value", "Allow")));

        list.Add(T("cap-radios", Permissoes,
            "Bloquear acesso de apps a rádios (Bluetooth/Wi-Fi)",
            "ConsentStore de radios = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\radios", "Value", "Deny"),
            BothSt(Consent + @"\radios", "Value", "Allow"),
            note: "Só faz sentido em tablet/celular; em desktop é inofensivo, mas sem efeito prático."));

        list.Add(T("cap-unpaired-devices", Permissoes,
            "Bloquear acesso de apps a dispositivos não pareados",
            "ConsentStore de bluetoothSync = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\bluetoothSync", "Value", "Deny"),
            BothSt(Consent + @"\bluetoothSync", "Value", "Allow")));

        list.Add(T("cap-documents", Permissoes,
            "Bloquear acesso de apps à pasta Documentos",
            "ConsentStore de documentsLibrary = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\documentsLibrary", "Value", "Deny"),
            BothSt(Consent + @"\documentsLibrary", "Value", "Allow")));

        list.Add(T("cap-pictures", Permissoes,
            "Bloquear acesso de apps à pasta Imagens",
            "ConsentStore de picturesLibrary = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\picturesLibrary", "Value", "Deny"),
            BothSt(Consent + @"\picturesLibrary", "Value", "Allow")));

        list.Add(T("cap-videos", Permissoes,
            "Bloquear acesso de apps à pasta Vídeos",
            "ConsentStore de videosLibrary = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\videosLibrary", "Value", "Deny"),
            BothSt(Consent + @"\videosLibrary", "Value", "Allow")));

        list.Add(T("cap-file-system", Permissoes,
            "Bloquear acesso de apps ao sistema de arquivos inteiro",
            "ConsentStore de broadFileSystemAccess = Deny.",
            Advice.Opcional, false,
            BothSt(Consent + @"\broadFileSystemAccess", "Value", "Deny"),
            BothSt(Consent + @"\broadFileSystemAccess", "Value", "Allow"),
            note: "Pode quebrar apps que leem pastas fora das bibliotecas (ex.: editores de foto)."));

        list.Add(T("cap-eye-tracking", Permissoes,
            "Bloquear acesso de apps ao rastreio ocular",
            "ConsentStore de gazeInput = Deny.",
            Advice.Recomendado, true,
            BothSt(Consent + @"\gazeInput", "Value", "Deny"),
            BothSt(Consent + @"\gazeInput", "Value", "Allow")));

        list.Add(T("background-apps", Permissoes,
            "Proibir apps de rodar em segundo plano",
            "GlobalUserDisabled = 1 e BackgroundAppGlobalToggle = 0.",
            Advice.Recomendado, true,
            Merge(BothDw(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1),
                BothDw(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0)),
            Merge(BothDw(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0),
                BothDel(@"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle")),
            explanation: "Impede apps da Store de continuar rodando com a janela fechada. " +
                         "Apps de desktop (Steam, Discord, CS2) não são afetados."));

        // -------------------------------------------------------------- Microsoft Edge
        list.Add(EdgeTweak("edge-do-not-track", "Desativar rastreio na web (Do Not Track)",
            "ConfigureDoNotTrack = 1 no Edge.", "ConfigureDoNotTrack", 1));
        list.Add(EdgeTweak("edge-payment-methods", "Não consultar métodos de pagamento salvos",
            "PaymentMethodQueryEnabled = 0.", "PaymentMethodQueryEnabled", 0));
        list.Add(EdgeTweak("edge-personalization", "Desativar personalização de anúncios e notícias",
            "PersonalizationReportingEnabled = 0.", "PersonalizationReportingEnabled", 0));
        list.Add(EdgeTweak("edge-address-bar-bing", "Desativar sugestões do Bing na barra de endereços",
            "AddressBarMicrosoftSearchInBingProviderEnabled = 0.", "AddressBarMicrosoftSearchInBingProviderEnabled", 0));
        list.Add(EdgeTweak("edge-user-feedback", "Desativar botão de feedback do Edge",
            "UserFeedbackAllowed = 0.", "UserFeedbackAllowed", 0));
        list.Add(EdgeTweak("edge-credit-card", "Não salvar cartão de crédito em sites",
            "AutofillCreditCardEnabled = 0.", "AutofillCreditCardEnabled", 0));
        list.Add(EdgeTweak("edge-autofill-address", "Desativar sugestões de formulários",
            "AutofillAddressEnabled = 0.", "AutofillAddressEnabled", 0));
        list.Add(EdgeTweak("edge-local-providers", "Desativar sugestões de provedores locais",
            "LocalProvidersEnabled = 0.", "LocalProvidersEnabled", 0));
        list.Add(EdgeTweak("edge-search-suggestions", "Desativar sugestões de busca e sites",
            "SearchSuggestEnabled = 0.", "SearchSuggestEnabled", 0));
        list.Add(EdgeTweak("edge-shopping", "Desativar o assistente de compras do Edge",
            "EdgeShoppingAssistantEnabled = 0.", "EdgeShoppingAssistantEnabled", 0));
        list.Add(EdgeTweak("edge-bar", "Desativar o Edge Bar",
            "WebWidgetAllowed = 0.", "WebWidgetAllowed", 0));
        list.Add(EdgeTweak("edge-sidebar", "Desativar a barra lateral do Edge",
            "HubsSidebarEnabled = 0.", "HubsSidebarEnabled", 0));
        list.Add(EdgeTweak("edge-nav-errors", "Não usar serviços web para resolver erros de navegação",
            "ResolveNavigationErrorsUseWebService = 0.", "ResolveNavigationErrorsUseWebService", 0));
        list.Add(EdgeTweak("edge-alternate-errors", "Desativar sugestão de sites parecidos em erro 404",
            "AlternateErrorPagesEnabled = 0.", "AlternateErrorPagesEnabled", 0));
        list.Add(EdgeTweak("edge-preload", "Desativar pré-carregamento de páginas",
            "NetworkPredictionOptions = 2.", "NetworkPredictionOptions", 2));
        list.Add(EdgeTweak("edge-passwords", "Desativar o gerenciador de senhas do Edge",
            "PasswordManagerEnabled = 0.", "PasswordManagerEnabled", 0));
        list.Add(EdgeTweak("edge-site-safety", "Desativar serviços de segurança de site",
            "SiteSafetyServicesEnabled = 0.", "SiteSafetyServicesEnabled", 0));
        list.Add(EdgeTweak("edge-smartscreen", "Desativar o filtro SmartScreen do Edge",
            "SmartScreenEnabled = 0.", "SmartScreenEnabled", 0, Advice.Evitar,
            "Não recomendado: é a principal defesa contra phishing e downloads maliciosos."));
        list.Add(EdgeTweak("edge-typosquatting", "Desativar o verificador de typosquatting",
            "TyposquattingCheckerEnabled = 0.", "TyposquattingCheckerEnabled", 0));

        // ------------------------------------------------------- Microsoft Edge (legado)
        list.Add(LegacyTweak("edge-legacy-dnt", "Edge legado: ativar Do Not Track",
            @"\Main", "DoNotTrack", 1));
        list.Add(LegacyTweak("edge-legacy-page-prediction", "Edge legado: desativar predição de páginas",
            @"\FlipAhead", "FPEnabled", 0));
        list.Add(LegacyTweak("edge-legacy-search-suggestions", "Edge legado: desativar sugestões de busca",
            @"\Main", "ShowSearchSuggestionsGlobal", 0));
        list.Add(LegacyTweak("edge-legacy-cortana", "Edge legado: desativar Cortana",
            @"\ServiceUI", "EnableCortana", 0));
        list.Add(LegacyTweak("edge-legacy-search-history", "Edge legado: não mostrar histórico de busca",
            @"\ServiceUI\ShowSearchHistory", "", 0));
        list.Add(LegacyTweak("edge-legacy-form-suggest", "Edge legado: desativar sugestões de formulário",
            @"\Main", "Use FormSuggest", 0, stringValue: true));
        list.Add(LegacyTweak("edge-legacy-media-licenses", "Edge legado: não salvar licenças de mídia protegida",
            @"\Privacy", "EnableEncryptedMediaExtensions", 0));
        list.Add(LegacyTweak("edge-legacy-screen-reader", "Edge legado: não otimizar busca para leitor de tela",
            @"\Main", "OptimizeWindowsSearchResultsForScreenReaders", 0));
        list.Add(LegacyTweak("edge-legacy-smartscreen", "Edge legado: desativar o filtro SmartScreen",
            @"\PhishingFilter", "EnabledV9", 0, Advice.Evitar,
            "Não recomendado: reduz a proteção contra sites falsos."));
        list.Add(LegacyTweak("edge-legacy-address-dropdown", "Edge legado: desativar autocompletar da barra de endereços",
            @"", "AllowAddressBarDropdown", 0, hklmPolicy: @"SOFTWARE\Microsoft\PolicyManager\current\device\Browser"));
        list.Add(LegacyTweak("edge-legacy-tab-preload", "Edge legado: não pré-carregar abas em segundo plano",
            @"", "AllowTabPreloading", 0, hklmPolicy: @"SOFTWARE\Policies\Microsoft\MicrosoftEdge\TabPreloader"));
        list.Add(LegacyTweak("edge-legacy-prelaunch", "Edge legado: não iniciar em segundo plano",
            @"", "AllowPrelaunch", 0, hklmPolicy: @"SOFTWARE\Policies\Microsoft\MicrosoftEdge\Main"));

        // ------------------------------------------------------------ Microsoft Office
        list.Add(OfficeTweak("office-telemetry", "Office: desativar telemetria",
            "DisableTelemetry = 1.", @"Software\Microsoft\Office\Common\ClientTelemetry", "DisableTelemetry", 1));
        list.Add(OfficeTweak("office-send-telemetry", "Office: não enviar dados de diagnóstico",
            "SendTelemetry = 3 (nenhum).", @"Software\Policies\Microsoft\Office\Common\ClientTelemetry", "SendTelemetry", 3));
        list.Add(OfficeTweak("office-ceip", "Office: sair do CEIP",
            "QMEnable = 0.", @"Software\Policies\Microsoft\Office\16.0\Common", "QMEnable", 0));
        list.Add(OfficeTweak("office-linkedin", "Office: desativar informações do LinkedIn",
            "LinkedIn = 0.", @"Software\Policies\Microsoft\Office\16.0\Common", "LinkedIn", 0));
        list.Add(OfficeTweak("office-inline-prediction", "Office: desativar previsão de texto em e-mails",
            "InlineTextPrediction = 1.", @"Software\Microsoft\Office\16.0\Common\MailSettings", "InlineTextPrediction", 1));
        list.Add(OfficeTweak("office-telemetry-log", "Office: desativar log do agente de telemetria",
            "Enablelogging = 0.", @"Software\Policies\Microsoft\Office\16.0\OSM", "Enablelogging", 0));
        list.Add(OfficeTweak("office-telemetry-upload", "Office: desativar upload do agente de telemetria",
            "EnableUpload = 0.", @"Software\Policies\Microsoft\Office\16.0\OSM", "EnableUpload", 0));
        list.Add(OfficeTweak("office-telemetry-obfuscate", "Office: ofuscar nomes de arquivo na telemetria",
            "EnableFileObfuscation = 1.", @"Software\Policies\Microsoft\Office\16.0\OSM", "EnableFileObfuscation", 1));
        list.Add(OfficeTweak("office-surveys", "Office: desativar pesquisas",
            "SurveyEnabled = 0.", @"Software\Policies\Microsoft\Office\16.0\Common\Feedback", "SurveyEnabled", 0));
        list.Add(OfficeTweak("office-feedback", "Office: desativar feedback para a Microsoft",
            "Enabled = 0.", @"Software\Policies\Microsoft\Office\16.0\Common\Feedback", "Enabled", 0));
        list.Add(OfficeTweak("office-feedback-email", "Office: não incluir e-mail no feedback",
            "IncludeEmail = 0.", @"Software\Policies\Microsoft\Office\16.0\Common\Feedback", "IncludeEmail", 0));
        list.Add(OfficeTweak("office-reliability", "Office: não enviar dados de confiabilidade",
            "UpdateReliabilityData = 0.", @"Software\Policies\Microsoft\Office\16.0\Common", "UpdateReliabilityData", 0));
        list.Add(OfficeTweak("office-disconnected", "Office: desativar experiências conectadas",
            "DisconnectedState = 0.", @"Software\Policies\Microsoft\Office\16.0\Common\Privacy", "DisconnectedState", 0));
        list.Add(OfficeTweak("office-content-analytics", "Office: desativar análise de conteúdo",
            "UserContentDisabled = 2.", @"Software\Policies\Microsoft\Office\16.0\Common\Privacy", "UserContentDisabled", 2));
        list.Add(OfficeTweak("office-download-content", "Office: não baixar conteúdo online",
            "DownloadContentDisabled = 2.", @"Software\Policies\Microsoft\Office\16.0\Common\Privacy", "DownloadContentDisabled", 2));
        list.Add(OfficeTweak("office-connected-services", "Office: desativar experiências conectadas opcionais",
            "ControllerConnectedServicesEnabled = 2.", @"Software\Policies\Microsoft\Office\16.0\Common\Privacy", "ControllerConnectedServicesEnabled", 2));

        // ---------------------------------------------- Sincronização de Configurações
        list.Add(SyncTweak("sync-all", "Desativar a sincronização de todas as configurações",
            "SyncPolicy = 0.", "SyncPolicy", 0, 5,
            "Não recomendado: desliga de uma vez a sincronização de tema, senhas, idioma e acessibilidade entre PCs."));
        list.Add(SyncTweak("sync-design", "Não sincronizar configurações de aparência",
            @"Groups\Personalization", "Enabled", 0, 1));
        list.Add(SyncTweak("sync-browser", "Não sincronizar configurações de navegador",
            @"Groups\BrowserSettings", "Enabled", 0, 1));
        list.Add(SyncTweak("sync-credentials", "Não sincronizar senhas entre PCs",
            @"Groups\Credentials", "Enabled", 0, 1,
            explanation: "Evita que credenciais salvas no Windows viajem pela conta Microsoft. " +
                         "Só afeta a sincronização; o login normal continua igual."));
        list.Add(SyncTweak("sync-language", "Não sincronizar configurações de idioma",
            @"Groups\Language", "Enabled", 0, 1));
        list.Add(SyncTweak("sync-accessibility", "Não sincronizar configurações de acessibilidade",
            @"Groups\Accessibility", "Enabled", 0, 1));
        list.Add(SyncTweak("sync-advanced", "Não sincronizar configurações avançadas do Windows",
            @"Groups\Windows", "Enabled", 0, 1));

        // ------------------------------------------------------------ Cortana e Busca
        list.Add(T("cortana-consent", Cortana,
            "Desativar e redefinir a Cortana",
            "CortanaConsent = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Windows Search", "CortanaConsent", 0)));

        list.Add(T("input-personalization", Cortana,
            "Desativar personalização de entrada",
            "Zera a política de privacidade e restringe coleta de tinta/texto.",
            Advice.Recomendado, true,
            Merge(BothDw(@"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0),
                BothDw(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1),
                BothDw(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1),
                BothDw(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", 0))));

        list.Add(T("online-speech", Cortana,
            "Desativar reconhecimento de fala online",
            "AllowInputPersonalization = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\InputPersonalization", "AllowInputPersonalization", 0)));

        list.Add(T("search-web-results", Cortana,
            "Não mostrar resultados da web na busca",
            "ConnectedSearchUseWeb = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0)));

        list.Add(T("speech-model-download", Cortana,
            "Não baixar modelos de fala",
            "ModelDownloadAllowed = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Speech_OneCore\Preferences", "ModelDownloadAllowed", 0)));

        list.Add(T("search-location", Cortana,
            "Proibir a busca de usar sua localização",
            "AllowSearchToUseLocation = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", 0)));

        list.Add(T("cortana-above-lock", Cortana,
            "Desativar a Cortana acima da tela de bloqueio",
            "AllowCortanaAboveLock = 0.",
            Advice.Opcional, false,
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", 0),
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", 1)));

        list.Add(T("cloud-search", Cortana,
            "Desativar busca na nuvem",
            "AllowCloudSearch = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0)));

        list.Add(T("web-search-desktop", Cortana,
            "Desativar busca na web pelo Windows Search",
            "DisableWebSearch = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Windows Search", "DisableWebSearch", 1)));

        // ------------------------------------------------- Telemetria e Comportamento
        list.Add(T("tailored-experiences", Comportamento,
            "Não usar dados de diagnóstico para experiências sob medida",
            "TailoredExperiencesWithDiagnosticDataEnabled = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)));

        list.Add(T("app-telemetry", Comportamento,
            "Desativar a telemetria de aplicativos",
            "AllowTelemetry = 0 e AITEnable = 0.",
            Advice.Recomendado, true,
            Merge(BothDw(@"Software\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0),
                BothDw(@"Software\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0),
                BothDw(@"Software\Policies\Microsoft\Windows\AppCompat", "AITEnable", 0)),
            explanation: "No Windows 10/11 Home o valor mínimo aceito é 1 (Básico); em Pro/Enterprise o 0 desliga de vez. " +
                         "Não mexe no Windows Update."));

        list.Add(T("diag-log-collection", Comportamento,
            "Limitar a coleta de logs de diagnóstico",
            "LimitDiagnosticLogCollection = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\DataCollection", "LimitDiagnosticLogCollection", 1)));

        list.Add(T("onesettings", Comportamento,
            "Não baixar configurações OneSettings",
            "DisableOneSettingsDownloads = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\DataCollection", "DisableOneSettingsDownloads", 1)));

        // ------------------------------------------------ Explorer e Barra de Tarefas
        list.Add(T("start-app-suggestions", Explorer,
            "Desativar sugestões de apps no menu Iniciar",
            "SystemPaneSuggestionsEnabled = 0.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SystemPaneSuggestionsEnabled", 0)));

        list.Add(T("jump-list-recent", Explorer,
            "Não mostrar itens recentes nas listas de atalhos",
            "Start_TrackDocs = 0 (Jump Lists do Iniciar e da barra).",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 0),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 1)));

        list.Add(T("explorer-ads", Explorer,
            "Desativar anúncios no Explorer/OneDrive",
            "ShowSyncProviderNotifications = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 1)));

        list.Add(T("onedrive-pre-signin", Explorer,
            "Impedir o OneDrive de usar a rede antes do login",
            "PreventNetworkTrafficPreUserSignIn = 1 (HKLM).",
            Advice.Recomendado, true,
            LmDw(@"SOFTWARE\Microsoft\OneDrive", "PreventNetworkTrafficPreUserSignIn", 1),
            LmDw(@"SOFTWARE\Microsoft\OneDrive", "PreventNetworkTrafficPreUserSignIn", 0)));

        list.Add(T("onedrive-disable", Explorer,
            "Desativar o Microsoft OneDrive",
            "DisableFileSyncNGSC = 1 (política).",
            Advice.Opcional, false,
            LmDw(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1),
            LmDw(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 0),
            note: "Não recomendado: quem usa OneDrive perde a sincronização de arquivos."));

        // ---------------------------------------------------------- Tela de Bloqueio
        list.Add(T("lockscreen-spotlight", TelaBloqueio,
            "Desativar o Windows Spotlight",
            "DisableWindowsSpotlightFeatures = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", 1)));

        list.Add(T("lockscreen-fun-facts", TelaBloqueio,
            "Desativar curiosidades e dicas na tela de bloqueio",
            "SubscribedContent-338387Enabled = 0.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SubscribedContent-338387Enabled", 0)));

        list.Add(T("lockscreen-notifications", TelaBloqueio,
            "Desativar notificações na tela de bloqueio",
            "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK", 0)));

        // ------------------------------------------------------------------- Diversos
        list.Add(T("feedback-reminders", Diversos,
            "Desativar lembretes de feedback",
            "Zera as regras de SIUF e marca DoNotShowFeedbackNotifications = 1.",
            Advice.Recomendado, true,
            Merge(BothDw(@"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0),
                BothDw(@"Software\Microsoft\Siuf\Rules", "PeriodInNanoSeconds", 0),
                BothDw(@"Software\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1))));

        list.Add(T("silent-store-apps", Diversos,
            "Não instalar apps recomendados da Store automaticamente",
            "SilentInstalledAppsEnabled = 0.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SilentInstalledAppsEnabled", 0)));

        list.Add(T("soft-landing", Diversos,
            "Desativar dicas e sugestões durante o uso do Windows",
            "SoftLandingEnabled = 0.",
            Advice.Recomendado, true,
            BothDw(Cdm, "SoftLandingEnabled", 0)));

        list.Add(T("wmp-diagnostics", Diversos,
            "Desativar diagnóstico do Windows Media Player",
            "UsageTracking = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\MediaPlayer\Preferences", "UsageTracking", 0)));

        list.Add(T("search-box-bing", Diversos,
            "Desativar a extensão Bing do Windows Search",
            "DisableSearchBoxSuggestions = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1),
            note: "Desliga de verdade as sugestões da web na barra de busca."));

        list.Add(T("people-taskbar", Diversos,
            "Desativar o ícone Pessoas na barra de tarefas",
            "PeopleBand = 0.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People", "PeopleBand", 0),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\People", "PeopleBand", 2)));

        list.Add(T("search-box-taskbar", Diversos,
            "Esconder a caixa de busca da barra de tarefas",
            "SearchboxTaskbarMode = 0 (só o ícone some; a busca continua no Iniciar).",
            Advice.Opcional, false,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 2)));

        list.Add(T("meet-now", Diversos,
            "Desativar o Meet Now na barra de tarefas",
            "HideSCAMeetNow = 1.",
            Advice.Recomendado, true,
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 0)));

        list.Add(T("news-interests", Diversos,
            "Desativar Notícias e Interesses na barra de tarefas",
            "ShellFeedsTaskbarViewMode = 2 e EnableFeeds = 0.",
            Advice.Opcional, false,
            Merge(BothDw(@"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2),
                BothDw(@"Software\Policies\Microsoft\Windows\Windows Feeds", "EnableFeeds", 0)),
            BothDw(@"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 0),
            note: "O Windows protege esses valores (nem administrador grava). Fica fora das recomendadas; use Barra de tarefas > Widgets nas Configurações."));

        list.Add(T("network-probing", Diversos,
            "Desativar a sonda de atividade de rede (NCSI)",
            "EnableActiveProbing = 0.",
            Advice.Evitar, false,
            LmDw(@"System\CurrentControlSet\Services\NlaSvc\Parameters\Internet", "EnableActiveProbing", 0),
            LmDw(@"System\CurrentControlSet\Services\NlaSvc\Parameters\Internet", "EnableActiveProbing", 1),
            note: "O próprio arquivo diz \"do not enable\": sem a sonda o ícone de rede pode mostrar \"sem internet\" mesmo com internet."));

        list.Add(T("pchc", Diversos,
            "Impedir a instalação do PC Health Check",
            "PreviousUninstall = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Microsoft\PCHC", "PreviousUninstall", 1)));

        list.Add(T("kms-online", Diversos,
            "Desativar a ativação online do KMS",
            "NoGenTicket = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows NT\CurrentVersion\Software Protection Platform", "NoGenTicket", 1)));

        list.Add(T("maps-auto-download", Diversos,
            "Desativar download automático de mapas",
            "AutoDownloadAndUpdateMapData = 0.",
            Advice.Opcional, false,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\Maps", "AutoDownloadAndUpdateMapData", 0),
            note: "Mapas offline deixam de ser atualizados sozinhos."));

        list.Add(T("maps-network", Diversos,
            "Desativar tráfego de rede na página de mapas offline",
            "AllowUntriggeredNetworkTrafficOnSettingsPage = 0.",
            Advice.Opcional, false,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\Maps", "AllowUntriggeredNetworkTrafficOnSettingsPage", 0),
            note: "Pode impedir o funcionamento dos mapas offline."));

        // ----------------------------------------------------------------- Localização
        list.Add(T("location-sensors", Localizacao,
            "Desativar sensores de localização e orientação",
            "DisableSensors = 1.",
            Advice.Opcional, false,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", 1),
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", 0)));

        list.Add(T("location-service", Localizacao,
            "Desativar a localização do sistema",
            "DisableLocation = 1 e DisableWindowsLocationProvider = 1 (HKLM).",
            Advice.Recomendado, true,
            Merge(LmDw(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableWindowsLocationProvider", 1))));

        list.Add(T("geolocation-service", Localizacao,
            "Desativar o serviço de geolocalização (lfsvc)",
            "Status = 0 e SensorPermissionState = 0.",
            Advice.Recomendado, true,
            Merge(LmDw(@"System\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", 0),
                LmDw(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}", "SensorPermissionState", 0)),
            Merge(LmDw(@"System\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", 1),
                LmDw(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Overrides\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}", "SensorPermissionState", 1))));

        list.Add(T("location-scripting", Localizacao,
            "Desativar scripts de localização",
            "DisableLocationScripting = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocationScripting", 1)));

        // ------------------------------------------------------- Defender e SpyNet
        list.Add(T("spynet", Defender,
            "Desativar a participação no Microsoft SpyNet",
            "SpyNetReporting = 0.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SpyNetReporting", 0),
            explanation: "A proteção em tempo real do Defender continua ligada; só a participação na comunidade SpyNet é desligada."));

        list.Add(T("defender-samples", Defender,
            "Não enviar amostras de arquivo para a Microsoft",
            "SubmitSamplesConsent = 2 (nunca enviar).",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent", 2)));

        list.Add(T("mrt-report", Defender,
            "Não reportar informações de infecção",
            "DontReportInfectionInformation = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\MRT", "DontReportInfectionInformation", 1)));

        // ------------------------------------------------------------------ Segurança
        list.Add(T("diagtrack-service", Seguranca,
            "Desativar o serviço de telemetria (DiagTrack)",
            "DiagTrack e dmwappushservice em Start = 4 e o autologger desligado.",
            Advice.Recomendado, true,
            Merge(LmDw(@"System\CurrentControlSet\Services\DiagTrack", "Start", 4),
                LmDw(@"System\CurrentControlSet\Services\dmwappushservice", "Start", 4),
                LmDw(@"System\CurrentControlSet\Control\WMI\Autologger\AutoLogger-Diagtrack-Listener", "Start", 0)),
            Merge(LmDw(@"System\CurrentControlSet\Services\DiagTrack", "Start", 2),
                LmDw(@"System\CurrentControlSet\Services\dmwappushservice", "Start", 3),
                LmDw(@"System\CurrentControlSet\Control\WMI\Autologger\AutoLogger-Diagtrack-Listener", "Start", 1)),
            reboot: true,
            explanation: "O serviço DiagTrack coleta e envia telemetria. Desligá-lo libera um pouco de CPU e disco, " +
                         "mas o efeito só aparece depois de reiniciar. Não afeta o Windows Update."));

        list.Add(T("steps-recorder", Seguranca,
            "Desativar o Gravador de Passos do Usuário",
            "DisableUAR = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\AppCompat", "DisableUAR", 1),
            note: "Desliga de verdade; o valor usado é o que funciona."));

        list.Add(T("password-reveal", Seguranca,
            "Desativar o botão de revelar senha no login",
            "DisablePasswordReveal = 1.",
            Advice.Opcional, false,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\CredUI", "DisablePasswordReveal", 1),
            note: "Não recomendado por usabilidade."));

        list.Add(T("wmdrm-online", Seguranca,
            "Desativar o acesso online do DRM de mídia",
            "DisableOnline = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\WMDRM", "DisableOnline", 1),
            note: "Desliga de verdade; o valor usado é o que funciona."));

        // ------------------------------------------------------------- Windows Update
        list.Add(T("speech-model-update", WindowsUpdate,
            "Não atualizar modelos de fala",
            "AllowSpeechModelUpdate = 0 (HKLM).",
            Advice.Recomendado, true,
            LmDw(@"SOFTWARE\Policies\Microsoft\Speech", "AllowSpeechModelUpdate", 0)));

        list.Add(T("update-p2p", WindowsUpdate,
            "Desativar o P2P do Windows Update",
            "DODownloadMode = 0 e SystemSettingsDownloadMode = 0.",
            Advice.Recomendado, true,
            Merge(LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 0),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0),
                CuDw(@"Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization", "SystemSettingsDownloadMode", 0)),
            Merge(LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 3)),
            explanation: "As atualizações continuam sendo baixadas normalmente pelo servidor da Microsoft; " +
                         "só para de compartilhar pedaços com outros PCs na internet."));

        list.Add(T("driver-updates", WindowsUpdate,
            "Não receber drivers pelo Windows Update",
            "ExcludeWUDriversInQualityUpdate = 1.",
            Advice.Opcional, false,
            LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "ExcludeWUDriversInQualityUpdate", 1),
            note: "Não recomendado: útil para segurar um driver ruim, mas você deixa de receber correções automáticas."));

        list.Add(T("defer-updates", WindowsUpdate,
            "Adiar atualizações de recursos e qualidade",
            "DeferUpgrade = 1, 1 mês para recursos e 4 semanas para qualidade.",
            Advice.Opcional, false,
            Merge(LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferUpgrade", 1),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferUpgradePeriod", 1),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "DeferUpdatePeriod", 4)),
            note: "Atrasa atualizações em vez de bloqueá-las. Bom para não tomar um update no meio de um campeonato."));

        list.Add(T("update-experimentation", WindowsUpdate,
            "Desativar configuração dinâmica e rollouts experimentais",
            "AllowExperimentation = 0 (HKLM).",
            Advice.Recomendado, true,
            LmDw(@"SOFTWARE\Microsoft\PolicyManager\current\device\System", "AllowExperimentation", 0),
            LmDw(@"SOFTWARE\Microsoft\PolicyManager\current\device\System", "AllowExperimentation", 1)));

        list.Add(T("store-app-auto-update", WindowsUpdate,
            "Não atualizar apps da Store automaticamente",
            "AutoDownload = 2.",
            Advice.Opcional, false,
            LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload", 2),
            LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore\WindowsUpdate", "AutoDownload", 4)));

        list.Add(T("device-metadata", WindowsUpdate,
            "Não baixar apps e ícones de fabricantes para dispositivos",
            "PreventDeviceMetadataFromNetwork = 1.",
            Advice.Recomendado, true,
            LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Device Metadata", "PreventDeviceMetadataFromNetwork", 1)));

        list.Add(T("other-products-updates", WindowsUpdate,
            "Não atualizar outros produtos da Microsoft pelo Windows Update",
            "RegisteredWithAU = 0.",
            Advice.Opcional, false,
            LmDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Services\7971f918-a847-4430-9279-4a52d1efe18d", "RegisteredWithAU", 0),
            note: "Não recomendado: Office e outros produtos deixam de ser atualizados por aqui."));

        list.Add(T("windows-update-service", WindowsUpdate,
            "Desativar o serviço do Windows Update",
            "wuauserv Start = 4 (desativado).",
            Advice.Evitar, false,
            LmDw(@"System\CurrentControlSet\Services\wuauserv", "Start", 4),
            LmDw(@"System\CurrentControlSet\Services\wuauserv", "Start", 3),
            reboot: true,
            note: "Não recomendado: você para de receber correções de segurança. Use só temporariamente e reative depois."));

        list.Add(T("update-notifications", WindowsUpdate,
            "Silenciar notificações do Windows Update",
            "SetUpdateNotificationLevel = 0, toast desligado e ícone fora da bandeja.",
            Advice.Opcional, false,
            Merge(LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", "SetUpdateNotificationLevel", 0),
                LmDw(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Windows.SystemToast.WindowsUpdate.Notification", "Enabled", 0),
                LmDw(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", "TrayIconVisibility", 0))));

        list.Add(T("no-auto-reboot", WindowsUpdate,
            "Impedir reinício automático com usuário logado",
            "NoAutoRebootWithLoggedOnUsers = 1 e AUOptions = 2 (avisar antes de baixar).",
            Advice.Recomendado, true,
            Merge(LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 0),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoRebootWithLoggedOnUsers", 1),
                LmDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", 2)),
            explanation: "O Windows deixa de reiniciar sozinho enquanto você está logado. " +
                         "As atualizações continuam baixando e instalando; só o reboot fica para você decidir."));

        // ------------------------------------------------- Copilot, Telefone e Widgets
        list.Add(T("copilot", Privacidade,
            "Desativar o Windows Copilot",
            "TurnOffWindowsCopilot = 1 (HKCU e HKLM).",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1),
            explanation: "Desliga o Copilot integrado ao Windows sem desinstalar nada. Não afeta o CS2 nem a Steam."));

        list.Add(T("copilot-recall", Privacidade,
            "Desativar o Copilot+ Recall (capturas automáticas de tela)",
            "DisableAIDataAnalysis = 1.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1),
            note: "Só tem efeito em PCs Copilot+; nos outros é inofensivo."));

        list.Add(T("copilot-button", Explorer,
            "Ocultar o botão do Copilot da barra de tarefas",
            "ShowCopilotButton = 0.",
            Advice.Recomendado, true,
            CuDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0),
            explanation: "Só esconde o botão; não desliga o Copilot por si só."));

        list.Add(T("phone-link", Privacidade,
            "Desativar o Link para Dispositivos Móveis (Telefone)",
            "EnableCdp = 0 (HKLM).",
            Advice.Recomendado, true,
            LmDw(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableCdp", 0),
            explanation: "Desliga a Plataforma de Dispositivos Conectados usada pelo app Link para Windows. " +
                         "Bluetooth e USB continuam funcionando."));

        list.Add(T("phone-suggestions", Privacidade,
            "Desativar sugestões de usar o app Telefone",
            "ShowPhoningPrompt = 0.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Microsoft\Windows\CurrentVersion\CloudExperienceHost", "ShowPhoningPrompt", 0)));

        list.Add(T("widgets", Explorer,
            "Desativar os Widgets do Windows 11",
            "AllowNewsAndInterests = 0.",
            Advice.Recomendado, true,
            BothDw(@"SOFTWARE\Microsoft\PolicyManager\current\device\NewsAndInterests", "AllowNewsAndInterests", 0),
            explanation: "Remove o painel de widgets (notícias e clima). A barra de tarefas e as notificações normais continuam funcionando."));

        list.Add(T("edge-privacy-pack", Edge,
            "Edge: cortar telemetria, personalização e sugestões",
            "Aplica as políticas de privacidade do Edge em um item só.",
            Advice.Recomendado, true,
            Merge(
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "ConfigureDoNotTrack", 1),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "UserFeedbackAllowed", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "SearchSuggestEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "PaymentMethodQueryEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "WebWidgetAllowed", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "AutofillCreditCardEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "LocalProvidersEnabled", 0),
                BothDw(@"SOFTWARE\Policies\Microsoft\Edge", "AddressBarMicrosoftSearchInBingProviderEnabled", 0)),
            note: "Só tem efeito se você usa o Microsoft Edge; não mexe em outros navegadores."));

        return list;
    }

    // ------------------------------------------------------------------ construtores

    private static Oosu10Tweak T(
        string id, string group, string title, string detail,
        Oosu10Advice advice, bool preset,
        Oosu10RegAction[] enable, Oosu10RegAction[]? restore = null,
        bool reboot = false, string note = "", string explanation = "")
    {
        return new Oosu10Tweak
        {
            Id = id,
            Group = group,
            Title = title,
            Detail = detail,
            Explanation = explanation,
            Note = note,
            Advice = advice,
            InPreset = preset,
            RequiresReboot = reboot,
            Enable = enable,
            Restore = restore ?? Array.Empty<Oosu10RegAction>()
        };
    }

    private static Oosu10Tweak EdgeTweak(
        string id, string title, string detail, string name, int value,
        Oosu10Advice advice = Oosu10Advice.Opcional, string note = "")
    {
        const string edge = @"SOFTWARE\Policies\Microsoft\Edge";
        return T(id, Edge, title, detail, advice, false,
            BothDw(edge, name, value),
            note: note.Length > 0 ? note : "Só tem efeito se você usa o Microsoft Edge; não mexe em outros navegadores.");
    }

    private static Oosu10Tweak LegacyTweak(
        string id, string title, string suffix, string name, int value,
        Oosu10Advice advice = Oosu10Advice.Opcional, string note = "",
        bool stringValue = false, string? hklmPolicy = null)
    {
        var path = hklmPolicy ?? LegacyEdge + suffix;
        var action = stringValue
            ? LmSt(path, name, "no")
            : LmDw(path, name, value);
        var detail = (string.IsNullOrEmpty(name) ? "(valor padrão)" : name) +
                     (stringValue ? " = no." : $" = {value}.");
        return T(id, EdgeLegado, title, detail, advice, false, action,
            note: note.Length > 0 ? note : "Só afeta o Microsoft Edge antigo (pré-Chromium); inofensivo se ele não existe.");
    }

    private static Oosu10Tweak OfficeTweak(
        string id, string title, string detail, string subKey, string name, int value)
    {
        return T(id, Office, title, detail, Advice.Opcional, false,
            BothDw(subKey, name, value),
            note: "Só tem efeito se o Microsoft Office estiver instalado.");
    }

    private static Oosu10Tweak SyncTweak(
        string id, string title, string suffix, string name, int value, int restore,
        string note = "", string explanation = "")
    {
        const string sync = @"Software\Microsoft\Windows\CurrentVersion\SettingSync";
        return T(id, Sincronizacao, title, $"{name} = {value}.", Advice.Opcional, false,
            BothDw(sync + suffix, name, value),
            BothDw(sync + suffix, name, restore),
            note: note, explanation: explanation);
    }

    // ------------------------------------------------------------------- helpers

    private static Oosu10RegAction Dw(string path, string name, int value) =>
        new(path, name, RegistryValueKind.DWord, value);

    private static Oosu10RegAction St(string path, string name, string value) =>
        new(path, name, RegistryValueKind.String, value);

    private static Oosu10RegAction[] BothDw(string subKey, string name, int value) =>
        new[] { Dw(Cu + subKey, name, value), Dw(Lm + subKey, name, value) };

    private static Oosu10RegAction[] BothSt(string subKey, string name, string value) =>
        new[] { St(Cu + subKey, name, value), St(Lm + subKey, name, value) };

    private static Oosu10RegAction[] BothDel(string subKey, string name) =>
        new[] { Del(Cu + subKey, name), Del(Lm + subKey, name) };

    private static Oosu10RegAction[] CuDw(string subKey, string name, int value) =>
        new[] { Dw(Cu + subKey, name, value) };

    private static Oosu10RegAction[] LmDw(string subKey, string name, int value) =>
        new[] { Dw(Lm + subKey, name, value) };

    private static Oosu10RegAction[] LmSt(string subKey, string name, string value) =>
        new[] { St(Lm + subKey, name, value) };

    private static Oosu10RegAction Del(string path, string name) =>
        new(path, name, RegistryValueKind.String, null);

    private static Oosu10RegAction[] Merge(params Oosu10RegAction[][] parts) =>
        parts.SelectMany(p => p).ToArray();
}
