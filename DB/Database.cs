using Server.DB;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static Server.DB.SQType;

namespace Server
{
    class Database
    {
        public const string SQFILE = "db.sqlite";
        public static SQLiteConnection sqCon;

        //creates all tables from SQRow nested types
        public static void SetupTables()
        {
            var tables = typeof(SQRow).GetNestedTypes();
            string ID = typeof(SQRow).GetFields()[0].Name;

            StringBuilder sb = new StringBuilder();

            foreach(var table in tables)
            {
                sb.Append("CREATE TABLE ").Append(table.Name).Append(" (");
                var fields = table.GetFields();
                string primaryKey = null, foreignKey = null, references = null;

                foreach(var field in fields)
                {
                    var attribute = field.GetCustomAttributes(false)[0];
                    Type attType = attribute.GetType();

                    sb.Append(field.Name).Append(" ").Append(attType.Name);

                    if (attribute is VARCHAR)
                    {
                        var cha = (VARCHAR)attribute;
                        sb.Append("(").Append(cha.lenght).Append(")");
                    }
                    else if(attribute is INT)
                    {
                        var i = (INT)attribute;
                        if (i.primaryKey)
                        {
                            sb.Append(" NOT NULL");
                            primaryKey = field.Name;
                        }
                        if(i.reference != null)
                        {
                            references = i.reference.Name;
                            foreignKey = field.Name;
                        }       
                    }

                    sb.Append(", ");
                }

                if(!string.IsNullOrEmpty(primaryKey))
                    sb.Append($"PRIMARY KEY({primaryKey}), ");

                if (!string.IsNullOrEmpty(foreignKey) && !string.IsNullOrEmpty(references))
                    sb.Append($"FOREIGN KEY({foreignKey}) REFERENCES {references}({ID}), ");

                sb.Remove(sb.Length - 2, 2); //remove last comma and whitespace

                sb.Append(");");
            }

            string s = sb.ToString();
            SQLiteCommand cmd = new SQLiteCommand(s, sqCon);
            cmd.ExecuteNonQuery();

            Console.WriteLine("New Database created.");
            if (Program.DEBUG)
                Console.WriteLine(s);
        }

        public static void ConnectDatabase()
        {
            bool newDB = false;
            if (!File.Exists(SQFILE))
            {
                SQLiteConnection.CreateFile(SQFILE);
                newDB = true;
            }

            sqCon = new SQLiteConnection($"Data Source={SQFILE};Version=3;");
            sqCon.Open();

            if (newDB)
                SetupTables();

            FindLastIds();

            Console.WriteLine("Database connection sucessfully established.");
        }

        static Dictionary<string, int> lastIds;
        private static void FindLastIds()
        {
            StringBuilder sb = new StringBuilder();
            var tables = typeof(SQRow).GetNestedTypes();
            lastIds = new Dictionary<string, int>();

            foreach (var table in tables)
                sb.Append($"SELECT MAX(id) AS Last_ID FROM {table.Name};");

            SQLiteCommand cmd = new SQLiteCommand(sb.ToString(), sqCon);
            SQLiteDataReader data = cmd.ExecuteReader();

            var intConverter = (TypeConverter)new INT();

            foreach (var table in tables)
            {
                if(data.Read())
                {
                    int i = (int)intConverter.FromSqlString(data["Last_ID"].ToString());
                    lastIds.Add(table.Name, i);
                }
            }

            if (Program.DEBUG)
                Console.WriteLine(lastIds.ToString());
        }

        public static int GetLastId(string table)
        {
            var result = -1;
            if(lastIds != null)
                result = lastIds[table];
            return result;
        }

        public static SQRow[] Select(string table, int startIndex, int lenght)
        {
            Type tableType = SQRow.GetTypeByName(table);
            string s = $"SELECT * FROM {tableType.Name} LIMIT {startIndex}, {lenght};";
            
            SQLiteCommand cmd = new SQLiteCommand(s, sqCon);
            SQLiteDataReader data = cmd.ExecuteReader();

            return ReadSQData(data, tableType);
        }

        public static SQRow Select(string table, int id)
        {
            Type tableType = SQRow.GetTypeByName(table);
            string s = $"SELECT * FROM {tableType.Name} WHERE id = {id};";

            SQLiteCommand cmd = new SQLiteCommand(s, sqCon);
            SQLiteDataReader data = cmd.ExecuteReader();

            var result = ReadSQData(data, tableType);
            if (result.Length > 0)
                return result[0];

            return null;
        }

        private static SQRow[] ReadSQData(SQLiteDataReader data, Type tableType)
        {
            List<SQRow> rows = new List<SQRow>();

            while (data.Read())
            {
                var fields = tableType.GetFields();
                object o = Activator.CreateInstance(tableType);

                foreach (var field in fields)
                {
                    var converter = (TypeConverter)field.GetCustomAttributes(false)[0];
                    var result = data[field.Name];
                    object sqlObj = converter.FromSqlString(result.ToString());
                    field.SetValue(o, sqlObj);
                }

                rows.Add((SQRow)o);
            }

            return rows.ToArray();
        }

        public static int Insert(SQRow table)
        {
            Type tableType = SQRow.GetTypeByName(table.GetType().Name);
            if (tableType == null)
                throw new Exception("Invalid table type.");

            StringBuilder sqString = new StringBuilder();
            StringBuilder values = new StringBuilder();

            sqString.Append("INSERT INTO ").Append(tableType.Name).Append(" (");
            values.Append("VALUES (");

            var fields = tableType.GetFields();
            foreach(var field in fields)
            {
                var att = field.GetCustomAttributes(false)[0];              
                var value = field.GetValue(table);

                if (value != null)
                {
                    var converter = (TypeConverter)att;
                    string sql = converter.ToSqlString(value);

                    sqString.Append(field.Name).Append(", ");
                    values.Append(sql).Append(", ");
                }
            }
            //remove last commas
            sqString.Remove(sqString.Length - 2, 2);
            values.Remove(values.Length - 2, 2);
            //bring everything together
            sqString.Append(") ").Append(values.ToString()).Append(");");

            if(Program.DEBUG)
                Console.WriteLine(sqString.ToString());

            SQLiteCommand cmd = new SQLiteCommand(sqString.ToString(), sqCon);

            int count = 0;
            try
            {
                count = cmd.ExecuteNonQuery();
            }
            catch { }

            return count;
        }

        public static int Update(SQRow table)
        {
            if (table.id == null) return 0;
            StringBuilder sb = new StringBuilder();
            Type tableType = table.GetType();
            var fields = tableType.GetFields();
            sb.Append("UPDATE ").Append(tableType.Name).Append(" SET ");

            foreach(var field in fields)
            {
                var value = field.GetValue(table);
                if(value != null)
                {
                    var converter = (TypeConverter)field.GetCustomAttributes(false)[0];
                    sb.Append(field.Name).Append(" = ").Append(converter.ToSqlString(value));
                    sb.Append(", ");
                }
            }

            sb.Remove(sb.Length - 2, 2);
            sb.Append(" WHERE id = ").Append(table.id).Append(";");

            if (Program.DEBUG)
                Console.WriteLine(sb.ToString());

            SQLiteCommand cmd = new SQLiteCommand(sb.ToString(), sqCon);
            return cmd.ExecuteNonQuery();
        }

        public static int Delete(string table, params int[] ids)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < ids.Length; i++)
                sb.Append($"DELETE FROM {table} WHERE id = {ids[i]};");

            if (Program.DEBUG)
                Console.WriteLine(sb.ToString());

            SQLiteCommand cmd = new SQLiteCommand(sb.ToString(), sqCon);
            return cmd.ExecuteNonQuery();
        }

        public static string CleanupString(string input)
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                if (char.IsLetterOrDigit(input[i]) || char.IsWhiteSpace(input[i]))
                    sb.Append(input[i]);
            }

            return sb.ToString();
        }

        public static void Close()
        {
            sqCon.Close();
        }

    }
}
