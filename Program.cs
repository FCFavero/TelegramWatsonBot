using IBM.Cloud.SDK.Core.Authentication.Iam;
using IBM.Cloud.SDK.Core.Http;
using IBM.Watson.Assistant.v2;
using IBM.Watson.Assistant.v2.Model;
using IBM.Watson.SpeechToText.v1;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramWatsonBot;
using Xabe.FFmpeg;
using System.IO;

class Program
{
    #region Variaveis de configuração
    static string telegramToken = string.Empty;

    // IBM 
    static string watsonApiKey = string.Empty;
    static string watsonUrl = string.Empty;
    static string assistantId = string.Empty;

    // IBM SPEECH TO TEXT 
    static string speechApiKey = string.Empty;
    static string speechUrl = string.Empty;
    static SpeechToTextService speechService = null!;
    
    static AssistantService assistant = null!;
    static string sessionId = string.Empty;
    #endregion

    static async Task Main()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        telegramToken = configuration["Telegram:BotToken"]!;
        watsonApiKey = configuration["WatsonAssistant:ApiKey"]!;
        watsonUrl = configuration["WatsonAssistant:Url"]!;
        assistantId = configuration["WatsonAssistant:AssistantId"]!;
        speechApiKey = configuration["WatsonSpeechToText:ApiKey"]!;
        speechUrl = configuration["WatsonSpeechToText:Url"]!;

        TelegramBotClient telegramBotClient = new TelegramBotClient(telegramToken);

        // IBM AUTH
        var authenticator = new IamAuthenticator(apikey: watsonApiKey);
        assistant = new AssistantService(version: "2021-06-14", authenticator: authenticator);
        assistant.SetServiceUrl(watsonUrl);

        // SESSION
        var session = assistant.CreateSession(assistantId: assistantId);
        sessionId = session.Result.SessionId;

        // IBM SPEECH TO TEXT AUTH
        var speechAuthenticator = new IamAuthenticator(apikey: speechApiKey);
        speechService = new SpeechToTextService(speechAuthenticator);
        speechService.SetServiceUrl(speechUrl);

        // Mensagem no console para monitoramento
        Console.WriteLine(MessageBot.StartBot);

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { }
        };

        telegramBotClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken: cts.Token);
        Console.ReadLine();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        MessageBot messageBot = new MessageBot();

        if (update.Type != UpdateType.Message)
            return;

        string texto = string.Empty;

        // TEXTO NORMAL
        if (update.Message!.Text != null)
        {
            texto = update.Message.Text;
            messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), $"Client: {texto}");
        }
        // ÁUDIO/VOZ
        else if (update.Message.Voice != null) 
        {
            try
            {
                await botClient.SendMessage(chatId: update.Message.Chat.Id, text: MessageBot.InterpretandoAudio, cancellationToken: cancellationToken);
                messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), $"{MessageBot.InterpretandoAudio} {texto}");

                var fileId = update.Message.Voice.FileId;
                var file = await botClient.GetFile(fileId, cancellationToken);
                string oggPath = Path.Combine(Path.GetTempPath(), $"{fileId}.ogg");
                string wavPath = Path.Combine(Path.GetTempPath(), $"{fileId}.wav");

                // BAIXA O ARQUIVO
                using (FileStream fs = new FileStream(oggPath, FileMode.Create))
                {
                    await botClient.DownloadFile(file.FilePath!, fs, cancellationToken);
                }

                // CONVERTE OGG → WAV - PEGA A PASTA RAIZ DA APLICAÇÃO
                string ffmpegPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin");
                FFmpeg.SetExecutablesPath(ffmpegPath);

                var conversion = await FFmpeg.Conversions.FromSnippet.Convert(oggPath, wavPath);
                await conversion.Start();

                // ENVIA PARA IBM SPEECH TO TEXT
                byte[] audioBytes = File.ReadAllBytes(wavPath);
                using var audioStream = new MemoryStream(audioBytes);
                var speechResult = speechService.Recognize(audio: audioStream, contentType: "audio/wav", model: "pt-BR_BroadbandModel");

                if (speechResult.Result.Results.Count > 0)
                {
                    texto = speechResult.Result.Results[0].Alternatives[0].Transcript;
                    messageBot.SendMessageConsole(fileId, $"{MessageBot.AudioReconhecido} {texto}");
                   
                    await botClient.SendMessage(chatId: update.Message.Chat.Id, text: $"{MessageBot.VoceDisse} {texto}", cancellationToken: cancellationToken);
                    messageBot.SendMessageConsole(fileId, $"{MessageBot.VoceDisse} {texto}");
                }
                else
                {
                    await botClient.SendMessage(chatId: update.Message.Chat.Id, text: MessageBot.NaoConseguiEntenderAudio, cancellationToken: cancellationToken);
                    messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), MessageBot.NaoConseguiEntenderAudio);
                    return;
                }

                // REMOVE ARQUIVOS TEMP
                if (File.Exists(oggPath))
                    File.Delete(oggPath);

                if (File.Exists(wavPath))
                    File.Delete(wavPath);
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(chatId: update.Message.Chat.Id, text: MessageBot.ErroProcessarAudio, cancellationToken: cancellationToken);
                messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), $"{MessageBot.ErroProcessarAudio} - Ex: {ex.Message}");
                return;
            }
        }
        else
            return;

        // COMANDO START
        if (texto.Equals(MessageBot.starte, StringComparison.CurrentCultureIgnoreCase))
        {
            await botClient.SendMessage(chatId: update.Message.Chat.Id, text: MessageBot.BemVindo, cancellationToken: cancellationToken);
            messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), MessageBot.BemVindo);
            return;
        }

        // WATSON ASSISTANT
        DetailedResponse<MessageResponse> response;

        response = assistant.Message(
            assistantId: assistantId,
            sessionId: sessionId,
            input: new MessageInput()
            {
                MessageType = "text",
                Text = texto
            }
        );

        var respostaWatson = response.Result.Output.Generic[0].Text;
        await botClient.SendMessage(chatId: update.Message.Chat.Id, text: respostaWatson, cancellationToken: cancellationToken);
        messageBot.SendMessageConsole(update.Message.Chat.Id.ToString(), respostaWatson);
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine(exception.Message);
        return Task.CompletedTask;
    }
}
