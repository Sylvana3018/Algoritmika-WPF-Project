using System.Data.OleDb;
namespace algoritm
{
    public static class DatabaseHelper
    {
        private static readonly string connectionString = "Provider=SQLOLEDB;Data Source=BLAFILD;Initial Catalog=algoritmika;Integrated Security=SSPI;Connect Timeout=30;";

        public static OleDbConnection GetConnection()
        {
            OleDbConnection connection = new OleDbConnection(connectionString);
            return connection;
        }
    }
}