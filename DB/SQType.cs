using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Server.DB
{
    public class SQType
    {
        public const string NULL = "NULL";

        public interface TypeConverter
        {
            string ToSqlString(object input);
            object FromSqlString(string input);
        }

        public class VARCHAR : Attribute, TypeConverter
        {
            public int lenght;
            public VARCHAR(int lenght)
            {
                this.lenght = lenght;
            }

            public object FromSqlString(string input)
            {
                return input;
            }

            public string ToSqlString(object input)
            {
                if (input == null)
                    return NULL;
                return $"'{input}'";
            }
        }

        public class INT : Attribute, TypeConverter
        {
            public bool primaryKey;
            public Type reference;

            public INT(Type reference = null, bool primaryKey = false)
            {
                this.primaryKey = primaryKey;
                this.reference = reference;
            }

            public object FromSqlString(string input)
            {
                int result = -1;
                int.TryParse(input, out result);
                return result;
            }

            public string ToSqlString(object input)
            {
                if (input == null)
                    return NULL;
                return input.ToString();
            }
        } 

        public class DATETIME : Attribute, TypeConverter
        {
            public object FromSqlString(string input)
            {
                DateTime dt = DateTime.Today;
                DateTime.TryParse(input, out dt);

                return dt;
            }

            public string ToSqlString(object input)
            {
                if (!(input is DateTime))
                    return null;
                DateTime dt = (DateTime)input;
                var str = dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                return str;
            }
        }


    }
}
