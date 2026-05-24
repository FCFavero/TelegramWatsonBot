namespace TelegramWatsonBot
{
    public class MessageBot
    {
        #region mesagens reservadas 
        public const string starte = "/start";    
        #endregion

        #region Mensagens 
        public const string StartBot = "Assistente virtual da Rotta, online!";
        public const string BemVindo = "Olá! 👋 Eu sou a Rottinha, assistente virtual da Rotta! Antes de começarmos a falar sobre pontos ou créditos de transporte, como eu posso te chamar?";
        public const string InterpretandoAudio = "🎤 Interpretando áudio...";
        public const string VoceDisse = "📝 Você disse:";
        public const string AudioReconhecido = "Áudio reconhecido:";
        #endregion

        #region mensagens de erro
        public const string NaoConseguiEntenderAudio = "Não consegui entender o áudio.";
        public const string ErroProcessarAudio = "Erro ao processar áudio.";
        #endregion

        public void SendMessageConsole(string id, string message)
        {
            string idMensagem = string.IsNullOrEmpty(id) ? "N/A" : id;
            string dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formattedMessage = $"Data: {dateTime} - [ID: {idMensagem}] {Environment.NewLine} {message}";
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine(formattedMessage);
        }
    }
}
