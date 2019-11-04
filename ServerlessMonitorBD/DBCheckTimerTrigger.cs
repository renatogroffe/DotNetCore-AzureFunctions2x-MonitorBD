using System;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ServerlessMonitorBD.Entities;

namespace ServerlessMonitorBD
{
    public static class DBCheckTimerTrigger
    {
        [FunctionName("DBCheckTimerTrigger")]
        public static void Run([TimerTrigger("*/30 * * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation(
                $"DBCheckTimerTrigger - iniciando execução em: {DateTime.Now}");

            var storageAccount = CloudStorageAccount
                .Parse(Environment.GetEnvironmentVariable("BaseLog"));
            var logTable = storageAccount
                .CreateCloudTableClient().GetTableReference("LogTable");

            if (logTable.CreateIfNotExistsAsync().Result)
                log.LogInformation("Criando a tabela de log...");

            var dadosLog = new LogEntity("BaseIndicadores",
                DateTime.Now.ToString("yyyyMMddHHmmss"));
            try
            {
                using (var conexao = new SqlConnection(
                    Environment.GetEnvironmentVariable("BaseIndicadores")))
                {
                    // Testa a validade da conexão abrindo a mesma
                    conexao.Open();
                    conexao.Close();

                    dadosLog.Status = "OK";
                }
            }
            catch (Exception ex)
            {
                dadosLog.Status = "Exception";
                dadosLog.DescricaoErro = ex.GetType().FullName + " | " +
                    ex.Message + " | " + ex.StackTrace;
            }

            var insertOperation = TableOperation.Insert(dadosLog);
            var resultInsert = logTable.ExecuteAsync(insertOperation).Result;
            string jsonResultInsert = JsonConvert.SerializeObject(resultInsert);
            if (dadosLog.Status == "OK")
                log.LogInformation(jsonResultInsert);
            else
            {
                log.LogError(jsonResultInsert);

                using (var clientLogicAppSlack = new HttpClient())
                {
                    clientLogicAppSlack.BaseAddress = new Uri(
                        Environment.GetEnvironmentVariable("UrlLogicAppAlerta"));
                    clientLogicAppSlack.DefaultRequestHeaders.Accept.Clear();
                    clientLogicAppSlack.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var requestMessage =
                          new HttpRequestMessage(HttpMethod.Post, String.Empty);

                    requestMessage.Content = new StringContent(
                        JsonConvert.SerializeObject(new
                        {
                            recurso = dadosLog.PartitionKey,
                            descricaoErro = dadosLog.DescricaoErro,
                        }), Encoding.UTF8, "application/json");

                    var respLogicApp = clientLogicAppSlack
                        .SendAsync(requestMessage).Result;
                    respLogicApp.EnsureSuccessStatusCode();

                    log.LogError(
                        "Envio de alerta para Logic App de integração com o Slack");
                }
            }

            log.LogInformation(
                $"DBCheckTimerTrigger - concluindo execução em: {DateTime.Now}");
        }
    }
}