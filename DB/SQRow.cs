using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using static Server.DB.SQType;

namespace Server
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    class SQRow
    {
        [INT(null, true)]
        public int id = -1;

        public static Type GetTypeByName(string name)
        {
            var types = typeof(SQRow).GetNestedTypes();
            name = name.ToLower();
            foreach (var type in types)
                if (type.Name.ToLower().Equals(name))
                    return type;
            return null;
        }

        public static SQRow ReadRow(BinaryReader reader)
        {
            try
            {
                string tableName = reader.ReadString();
                Type tableType = GetTypeByName(tableName);
                object row = Activator.CreateInstance(tableType);

                var fields = tableType.GetFields();

                foreach (var field in fields)
                {
                    Type varType = field.FieldType;
                    object data = null;

                    if (varType == typeof(string))
                        data = reader.ReadString();
                    else if (varType == typeof(int))
                        data = reader.ReadInt32();
                    else if (varType == typeof(DateTime))
                    {
                        string date = reader.ReadString();
                        data = new DATETIME().FromSqlString(date);
                    }

                    if (data != null)
                        field.SetValue(row, data);
                }

                return (SQRow)row;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteRow(BinaryWriter writeTo, SQRow row)
        {
            try
            {
                Type tableType = row.GetType();

                string name = tableType.Name;
                writeTo.Write(name);

                var fields = tableType.GetFields();
                foreach (var field in fields)
                {
                    object obj = field.GetValue(row);
                    Type t = field.FieldType;

                    if (t == typeof(string))
                        writeTo.Write(obj == null ? "" : (string)obj);
                    else if (t == typeof(int))
                        writeTo.Write((int)obj);
                    else if (t == typeof(DateTime))
                        writeTo.Write(new DATETIME().ToSqlString(obj));

                }
            }
            catch { }
        }

        public class Script : SQRow
        {
            [VARCHAR(15)]
            public string name;
        }

        public class Action : SQRow
        {
            [VARCHAR(15)]
            public string name;
            [INT]
            public int mintime;
            [INT]
            public int maxtime;
            [INT(typeof(Script))]
            public int script;
        }

        public class Cmd : SQRow
        {
            [VARCHAR(15)]
            public string cmd;
            [INT(typeof(Action))]
            public int action;
        }

        public class Account : SQRow
        {
            [VARCHAR(30)]
            public string email;
            [VARCHAR(12)]
            public string name;
            [VARCHAR(20)]
            public string password;
            [DATETIME]
            public DateTime status;
            [INT(typeof(Action))]
            public int action;
        }

        public class Proxy : SQRow
        {
            [VARCHAR(15)]
            public string ip;
            [VARCHAR(5)]
            public string port;
            [INT(typeof(Account))]
            public int claimedBy;
        }

    }
}
