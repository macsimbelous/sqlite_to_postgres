using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.Data;
using Npgsql;

namespace sqlite_to_postgres
{
    class Program
    {
        static public SQLiteConnection connection = null;
        static public SQLiteTransaction transaction = null;
        static public NpgsqlConnection npgsqlconn = null;
        static void Main(string[] args)
        {
            List<CImage> img_list = new List<CImage>();
            #region ReadSqlite
            using (SQLiteConnection connection = new SQLiteConnection(@"data source=C:\Users\macs\Dropbox\Backup\erza.sqlite"))
            {
                connection.Open();
                using (SQLiteCommand command = new SQLiteCommand())
                {

                    command.CommandText = "select * from hash_tags";
                    command.Connection = connection;
                    SQLiteDataReader reader = command.ExecuteReader();
                    int count = 0;
                    while (reader.Read())
                    {
                        CImage img = new CImage();
                        img.hash = (byte[])reader["hash"];
                        img.hash_str = BitConverter.ToString(img.hash).Replace("-", string.Empty).ToLower();
                        img.is_deleted = (bool)reader["is_deleted"];
                        if (!System.Convert.IsDBNull(reader["tags"]))
                        {
                            img.tags_string = (string)reader["tags"];
                        }
                        if (!Convert.IsDBNull(reader["file_name"]))
                        {
                            img.file = (string)reader["file_name"];
                        }
                        img_list.Add(img);
                        count++;
                        Console.Write("\r" + count.ToString("#######"));
                    }
                    reader.Close();
                    Console.WriteLine("\rВсего: " + (count++).ToString());
                }
            }
            #endregion
            npgsqlconn = new NpgsqlConnection("Server=127.0.0.1;Port=5432;User Id=Erza;Password=48sf54ro;Database=Erza;");
            npgsqlconn.Open();
            ExportTagsToMariaDB(img_list);
            ExportImagesToMariaDB(img_list);
            ExportImageTagsToMariaDB(img_list);
            npgsqlconn.Close();
        }
        static void ExportTagsToMariaDB(List<CImage> img_list)
        {
            Console.WriteLine("Получаем уникальные теги");
            List<string> all_tags = new List<string>();
            foreach (CImage img in img_list)
            {
                if (img.tags.Count > 0)
                {
                    all_tags.AddRange(img.tags);
                }
            }
            all_tags = all_tags.Distinct().ToList();
            all_tags.Sort();
            Console.WriteLine("\nТегов: " + all_tags.Count.ToString());
            Console.WriteLine("Загружаем теги в Базуданных");
            for (int i = 0; i < all_tags.Count; i++)
            {
                //AddTagDB_MySql(all_tags[i]);
                AddTagDB_not_verify_MySql(all_tags[i]);
                Console.Write("\rДобавлено: {0}", i.ToString("000000"));
            }
            Console.WriteLine();
        }
        static void ExportImagesToMariaDB(List<CImage> img_list)
        {
            Console.WriteLine();
            for (int i = 0; i < img_list.Count; i++)
            {
                string ins = "INSERT INTO images (is_deleted, hash, file_path) VALUES (:is_deleted, :hash, :file_path);";
                using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                {
                    ins_command.Parameters.AddWithValue("hash", NpgsqlTypes.NpgsqlDbType.Varchar, img_list[i].hash_str);
                    ins_command.Parameters.AddWithValue("is_deleted", NpgsqlTypes.NpgsqlDbType.Boolean, img_list[i].is_deleted);
                    if (string.IsNullOrEmpty(img_list[i].file))
                    {
                        ins_command.Parameters.AddWithValue("file_path", NpgsqlTypes.NpgsqlDbType.Varchar, System.DBNull.Value);
                    }
                    else
                    {
                        ins_command.Parameters.AddWithValue("file_path", NpgsqlTypes.NpgsqlDbType.Varchar, img_list[i].file);
                    }
                    ins_command.ExecuteNonQuery();
                }
                Console.Write("\rДобавляем картинки: {0}", i.ToString("######"));
            }
        }
        static void UpdateUriToMariaDB(List<CImage> img_list)
        {
            Console.WriteLine();
            for (int i = 0; i < img_list.Count; i++)
            {
                string ins = "UPDATE images SET uri = @uri WHERE hash = @hash";
                using (NpgsqlCommand command = new NpgsqlCommand(ins, npgsqlconn))
                {
                    command.Parameters.AddWithValue("hash", img_list[i].hash);
                    if (string.IsNullOrEmpty(img_list[i].file))
                    {
                        command.Parameters.AddWithValue("uri", System.DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue("uri", img_list[i].file);
                    }
                    command.ExecuteNonQuery();
                }
                Console.Write("\rОбновляем картинки: {0}", i.ToString("######"));
            }
        }
        static void ExportImageTagsToMariaDB(List<CImage> img_list)
        {
            Console.WriteLine("Формируем image_tags");
            List<image_tags> it = new List<image_tags>();
            int count = 0;
            foreach (CImage img in img_list)
            {
                if (img.tags.Count > 0)
                {
                    List<long> tag_ids = GetTagIDs(img.tags);
                    InsertImageTagsMass(GetImageID(img.hash_str), tag_ids);
                }
                count++;
                Console.Write("{0}\\{1}\r", count, img_list.Count);
            }
            Console.WriteLine("Размер image_tags: {0}\n", it.Count);
        }
        static void InsertImageTagsMass(long image_id, List<long> tag_ids)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT INTO image_tags (image_id, tag_id) VALUES ");
            for (int i = 0; i < tag_ids.Count; i++)
            {
                if (i > 0) sql.Append(", ");
                sql.Append("(" + image_id.ToString() + ", " + tag_ids[i].ToString() + ")");
            }
            sql.Append(";");
            using (NpgsqlCommand ins_command = new NpgsqlCommand(sql.ToString(), npgsqlconn))
            {
                ins_command.ExecuteNonQuery();
            }
        }
        static List<long> GetTagIDs(List<string> tags)
        {
            List<long> ids = new List<long>();
            StringBuilder ins_quwery = new StringBuilder("SELECT tag_id FROM tags WHERE ");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("tag = '");
                    ins_quwery.Append(tags[i].Replace("\'", "\'\'"));
                    ins_quwery.Append("'");
                }
                else
                {
                    ins_quwery.Append(" OR tag = '");
                    ins_quwery.Append(tags[i].Replace("\'", "\'\'"));
                    ins_quwery.Append("'");
                }
            }
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), npgsqlconn))
            {
                NpgsqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ids.Add((long)reader[0]);
                }
                reader.Close();
            }
            return ids;
        }
        static long GetImageID(string hash)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT image_id FROM images WHERE hash = :hash;", npgsqlconn))
            {
                command.Parameters.Add("hash", NpgsqlTypes.NpgsqlDbType.Varchar, 32).Value = hash;
                return (long)command.ExecuteScalar();
            }
        }
        public static void AddTagDB_not_verify_MySql(string tag)
        {
            string ins = "INSERT INTO tags (tag) VALUES (:tag);";
            using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
            {
                ins_command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Varchar, 128).Value = tag;
                ins_command.ExecuteNonQuery();
            }
        }
    }
    public class CImage
    {
        public long image_id;
        public long file_id;
        public bool is_new = true;
        public bool is_deleted = false;
        public long id;
        public byte[] hash;
        public string file = null;
        public List<string> tags = new List<string>();
        public string hash_str;
        public string tags_string
        {
            get
            {
                string s = String.Empty;
                for (int i = 0; i < tags.Count; i++)
                {
                    if (i > 0)
                    {
                        s = s + " ";
                    }
                    s = s + tags[i];
                }
                return s;
            }
            set
            {
                string[] t = value.Split(' ');
                for (int i = 0; i < t.Length; i++)
                {
                    if (t[i].Length > 0)
                    {
                        tags.Add(t[i]);
                    }
                }
            }
        }
        public override string ToString()
        {
            if (this.file != String.Empty)
            {
                return file.Substring(file.LastIndexOf('\\') + 1);
            }
            else
            {
                return "No File!";
            }
        }
    }
    public class image_tags
    {
        public int tag_id;
        public int image_id;
        public image_tags(int _tag_id, int _image_id)
        {
            this.tag_id = _tag_id;
            this.image_id = _image_id;
        }
    }
}
