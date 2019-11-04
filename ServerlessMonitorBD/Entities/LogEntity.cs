using Microsoft.WindowsAzure.Storage.Table;

namespace ServerlessMonitorBD.Entities
{
    public class LogEntity : TableEntity
    {
        public LogEntity(string baseDados, string horario)
        {
            this.PartitionKey = baseDados;
            this.RowKey = horario;
        }

        public LogEntity() { }

        public string Status { get; set; }
        public string DescricaoErro { get; set; }
    }
}