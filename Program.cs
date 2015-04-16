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
            List<string> all_tags = new List<string>();
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
            //UpdateUriToMariaDB(img_list);
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

            //all_tags = GetAllUntiqeTags(img_list);
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
                //NewImageDB_MySql(img_list[i]);
                string ins = "INSERT IGNORE INTO images (hash, is_deleted, uri) VALUES (@hash, @is_deleted, @uri);";
                using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                {
                    ins_command.Parameters.AddWithValue("hash", img_list[i].hash);
                    ins_command.Parameters.AddWithValue("is_deleted", img_list[i].is_deleted);
                    if (string.IsNullOrEmpty(img_list[i].file))
                    {
                        ins_command.Parameters.AddWithValue("uri", System.DBNull.Value);
                    }
                    else
                    {
                        ins_command.Parameters.AddWithValue("uri", img_list[i].file);
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
            foreach (CImage img in img_list)
            {
                if (img.tags.Count > 0)
                {
                    List<int> tag_ids = GetTagIDs(img.tags);
                    InsertImageTagsMass((int)img.id, tag_ids);
                }
            }
            Console.WriteLine("Размер image_tags: {0}\n", it.Count);
        }
        static void InsertImageTagsMass(int image_id, List<int> tag_ids)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("INSERT IGNORE INTO image_tags (image_id, tag_id) VALUES ");
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
        static List<int> GetTagIDs(List<string> tags)
        {
            List<int> ids = new List<int>();
            StringBuilder ins_quwery = new StringBuilder("SELECT tag_id FROM tags WHERE ");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("tag = '");
                    ins_quwery.Append(tags[i]);
                    ins_quwery.Append("'");
                }
                else
                {
                    ins_quwery.Append(" OR tag = '");
                    ins_quwery.Append(tags[i]);
                    ins_quwery.Append("'");
                }
            }
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), npgsqlconn))
            {
                NpgsqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ids.Add((int)reader[0]);
                }
                reader.Close();
            }
            return ids;
        }
        static List<string> GetAllUntiqeTags(List<CImage> img_list)
        {
            List<string> UntiqeTags = new List<string>();
            UntiqeTags.Sort();
            int CountTags = 0;
            foreach (CImage img in img_list)
            {
                foreach (string tag in img.tags)
                {
                    int index = UntiqeTags.BinarySearch(tag);
                    if (index < 0)
                    {
                        UntiqeTags.Insert(~index, tag);
                        CountTags++;
                        Console.Write("\r" + CountTags.ToString("#######"));
                    }
                }
            }
            return UntiqeTags;
        }
        #region MySql
        public static void AddFileDB_MySql(long image_id, string new_file)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM files WHERE (file_name = @file_name)", npgsqlconn))
            {
                command.Parameters.AddWithValue("file_name", new_file);
                object o = command.ExecuteScalar();
                if (o == null)
                {
                    string ins = "INSERT INTO files (image_id, file_name) VALUES (@image_id, @file_name); select last_insert_rowid();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                    {

                        ins_command.Parameters.Add("file_name", NpgsqlTypes.NpgsqlDbType.Text, 4000).Value = new_file;
                        ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                        System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }
        }
        public static void UpdateImageDB_MySql(long image_id, CImage new_image)
        {
            string update = "UPDATE images SET is_new = @is_new, is_deleted = @is_deleted WHERE image_id = @image_id";
            using (NpgsqlCommand command = new NpgsqlCommand(update, npgsqlconn))
            {
                command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                command.Parameters.Add("is_new", NpgsqlTypes.NpgsqlDbType.Bit).Value = new_image.is_new;
                command.Parameters.Add("is_deleted", NpgsqlTypes.NpgsqlDbType.Bit).Value = new_image.is_deleted;
                command.ExecuteNonQuery();
            }
        }
        public static void add_image_tags_MySql(long tag_id, long image_id)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT tag_id, image_id FROM image_tags WHERE (image_id = @image_id) AND (tag_id = @tag_id)", npgsqlconn))
            {
                command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                object o = command.ExecuteScalar();
                if (o == null)
                {
                    string ins = "INSERT INTO image_tags (tag_id, image_id) VALUES ( @tag_id, @image_id)";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                    {
                        ins_command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                        ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                        ins_command.ExecuteNonQuery();
                    }
                }
            }
        }
        public static void add_image_tags_not_verify_MySql(long tag_id, long image_id)
        {
            string ins = "INSERT INTO image_tags (tag_id, image_id) VALUES ( @tag_id, @image_id)";
            using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
            {
                ins_command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                ins_command.ExecuteNonQuery();
            }
        }
        public static long AddTagDB_MySql(string tag)
        {
            long t;
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT tag_id FROM tags WHERE (tag = @tag)", npgsqlconn))
            {
                command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Text, 128).Value = tag;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    t = System.Convert.ToInt64(o);
                }
                else
                {
                    string ins = "INSERT INTO tags (tag) VALUES (@tag); SELECT LAST_INSERT_ID();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                    {
                        ins_command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Text, 128).Value = tag;
                        t = System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }

            return t;
        }
        public static void AddTagDB_not_verify_MySql(string tag)
        {
            string ins = "INSERT IGNORE INTO tags (tag) VALUES (@tag);";
            using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
            {
                ins_command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Text, 128).Value = tag;
                ins_command.ExecuteNonQuery();
            }
        }
        public static void NewImageDB_MySql(CImage img)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT image_id FROM images WHERE (hash = @hash)", npgsqlconn))
            {
                command.Parameters.Add("hash", NpgsqlTypes.NpgsqlDbType.Text).Value = img.hash_str;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    long image_id = System.Convert.ToInt64(o);
                    if (img.file != null)
                    {
                        AddFileDB_MySql(image_id, img.file);
                    }
                    //UpdateImageDB_MySql(image_id, img);
                    for (int i = 0; i < img.tags.Count; i++)
                    {
                        long tag_id = AddTagDB_MySql(img.tags[i]);
                        add_image_tags_MySql(tag_id, image_id);
                        //add_image_tags_not_verify_MySql(tag_id, image_id);
                    }
                }
                else
                {
                    string ins = "INSERT INTO images (hash, is_new, is_deleted) VALUES (@hash, @is_new, @is_deleted); SELECT LAST_INSERT_ID();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, npgsqlconn))
                    {
                        ins_command.Parameters.Add("hash", NpgsqlTypes.NpgsqlDbType.Text).Value = img.hash_str;
                        ins_command.Parameters.Add("is_new", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_new;
                        ins_command.Parameters.Add("is_deleted", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_deleted;
                        long image_id = System.Convert.ToInt64(ins_command.ExecuteScalar());
                        for (int i = 0; i < img.tags.Count; i++)
                        {
                            long tag_id = AddTagDB_MySql(img.tags[i]);
                            add_image_tags_not_verify_MySql(tag_id, image_id);
                        }
                        /*if (img.tags.Count > 0)
                        {
                            List<long> tag_ids = get_tag_ids_MySql(img.tags);
                            add_image_tags_mass_not_verify_MySql(tag_ids, image_id);
                        }*/
                    }
                }
            }
            return;
        }
        public static void add_image_tags_mass_not_verify_MySql(List<long> tag_ids, long image_id)
        {
            StringBuilder ins_quwery = new StringBuilder("INSERT INTO image_tags (tag_id, image_id) VALUES ");
            for (int i = 0; i < tag_ids.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
                else
                {
                    ins_quwery.Append(", ");
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
            }
            ins_quwery.Append(";");
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), npgsqlconn))
            {
                command.ExecuteNonQuery();
            }
            return;
        }
        public static List<int> get_tag_ids_MySql(List<string> tags)
        {
            List<int> ids = new List<int>();
            StringBuilder ins_quwery = new StringBuilder("SELECT tag_id FROM tags WHERE ");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("tag = '");
                    tags[i] = tags[i].Replace("\\", "\\\\");
                    ins_quwery.Append(tags[i].Replace("'", "\\'"));
                    ins_quwery.Append("'");
                }
                else
                {
                    ins_quwery.Append(" OR tag = '");
                    tags[i] = tags[i].Replace("\\", "\\\\");
                    ins_quwery.Append(tags[i].Replace("'", "\\'"));
                    ins_quwery.Append("'");
                }
            }
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), npgsqlconn))
            {
                NpgsqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    //object temp = reader[0];
                    //int t = (int)temp;
                    ids.Add((int)reader[0]);
                }
                reader.Close();
            }
            return ids;
        }
        #endregion
        #region SQLite
        public static long AddFileDB_sqlite(long image_id, CImage new_image)
        {
            long id;
            using (SQLiteCommand command = new SQLiteCommand("SELECT * FROM files WHERE (file_name = @file_name)", sqlite_to_postgres.Program.connection))
            {
                command.Parameters.Add("file_name", DbType.String).Value = new_image.file;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    id = System.Convert.ToInt64(o);
                }
                else
                {

                    string ins = "INSERT INTO files (image_id, file_name) VALUES (@image_id, @file_name); select last_insert_rowid();";
                    using (SQLiteCommand ins_command = new SQLiteCommand(ins, sqlite_to_postgres.Program.connection))
                    {

                        ins_command.Parameters.Add("file_name", DbType.String, 4000).Value = new_image.file;
                        ins_command.Parameters.Add("image_id", DbType.UInt64).Value = image_id;
                        id = System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }
            return id;
        }
        public static void UpdateImageDB_sqlite(long image_id, CImage new_image)
        {
            string update = "UPDATE files SET file_name = @file_name WHERE image_id = @image_id";
            using (SQLiteCommand command = new SQLiteCommand(update, sqlite_to_postgres.Program.connection))
            {
                command.Parameters.Add("image_id", DbType.Int64).Value = image_id;
                command.Parameters.Add("file_name", DbType.String, 4000, "file_name").Value = new_image.file;
                command.ExecuteNonQuery();
            }
        }
        public static void add_image_tags_sqlite(long tag_id, long image_id)
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT tag_id, image_id FROM image_tags WHERE (image_id = @image_id) AND (tag_id = @tag_id)", connection))
            {
                command.Parameters.Add("image_id", DbType.Int64).Value = image_id;
                command.Parameters.Add("tag_id", DbType.Int64).Value = tag_id;
                object o = command.ExecuteScalar();
                if (o == null)
                {
                    string ins = "INSERT INTO image_tags (tag_id, image_id) VALUES ( @tag_id, @image_id)";
                    using (SQLiteCommand ins_command = new SQLiteCommand(ins, sqlite_to_postgres.Program.connection))
                    {
                        ins_command.Parameters.Add("tag_id", DbType.Int64).Value = tag_id;
                        ins_command.Parameters.Add("image_id", DbType.Int64).Value = image_id;
                        ins_command.ExecuteNonQuery();
                    }
                }
            }
            return;
        }
        public static void add_image_tags_sqlite_not_verify(List<long> tag_ids, long image_id)
        {
            StringBuilder ins_quwery = new StringBuilder("INSERT INTO image_tags (tag_id, image_id) VALUES ");
            for (int i = 0; i < tag_ids.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
                else
                {
                    ins_quwery.Append(", ");
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
            }
            ins_quwery.Append(";");
            using (SQLiteCommand command = new SQLiteCommand(ins_quwery.ToString(), connection))
            {
                command.ExecuteNonQuery();
            }
            return;
        }
        public static List<long> get_tag_ids(List<string> tags)
        {
            List<long> ids = new List<long>();
            StringBuilder ins_quwery = new StringBuilder("SELECT tag_id FROM tags WHERE ");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("tag = '");
                    ins_quwery.Append(tags[i].Replace("'", "''"));
                    ins_quwery.Append("'");
                }
                else
                {
                    ins_quwery.Append(" OR tag = '");
                    ins_quwery.Append(tags[i].Replace("'", "''"));
                    ins_quwery.Append("'");
                }
            }
            using (SQLiteCommand command = new SQLiteCommand(ins_quwery.ToString(), sqlite_to_postgres.Program.connection))
            {
                SQLiteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ids.Add((long)reader[0]);
                }
                reader.Close();
            }
            return ids;
        }
        public static long AddTagDB_sqlite(string tag)
        {
            long t;
            using (SQLiteCommand command = new SQLiteCommand("SELECT tag_id FROM tags WHERE (tag = @tag)", sqlite_to_postgres.Program.connection))
            {
                command.Parameters.Add("tag", DbType.StringFixedLength, 128).Value = tag;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    t = System.Convert.ToInt64(o);
                }
                else
                {
                    string ins = "INSERT INTO tags (tag) VALUES (@tag); select last_insert_rowid();";
                    using (SQLiteCommand ins_command = new SQLiteCommand(ins, sqlite_to_postgres.Program.connection))
                    {
                        ins_command.Parameters.Add("tag", DbType.StringFixedLength, 128).Value = tag;
                        t = System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }
            return t;
        }
        public static void NewImageDB_sqlite(CImage img)
        {
            using (SQLiteCommand command = new SQLiteCommand("SELECT image_id FROM images WHERE (hash = @hash)", sqlite_to_postgres.Program.connection))
            {
                command.Parameters.Add("hash", DbType.Binary).Value = img.hash;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    long image_id = System.Convert.ToInt64(o);
                    if (img.file != null)
                    {
                        AddFileDB_sqlite(image_id, img);
                    }
                    UpdateImageDB_sqlite(image_id, img);
                    for (int i = 0; i < img.tags.Count; i++)
                    {
                        long tag_id = AddTagDB_sqlite(img.tags[i]);
                        add_image_tags_sqlite(tag_id, image_id);
                    }
                }
                else
                {
                    string ins = "INSERT INTO images (hash) VALUES (@hash); select last_insert_rowid();";
                    using (SQLiteCommand ins_command = new SQLiteCommand(ins, sqlite_to_postgres.Program.connection))
                    {
                        ins_command.Parameters.Add("hash", DbType.Binary).Value = img.hash;
                        long image_id = System.Convert.ToInt64(ins_command.ExecuteScalar());
                        /*for (int i = 0; i < img.tags.Count; i++)
                        {
                            long tag_id = AddTagDB_sqlite(img.tags[i]);
                            add_image_tags_sqlite(tag_id, image_id);
                        }
                        if (img.file != null)
                        {
                            AddFileDB_sqlite(image_id, img);
                        }*/
                        List<long> tag_ids = get_tag_ids(img.tags);
                        add_image_tags_sqlite_not_verify(tag_ids, image_id);
                    }
                }
            }
            return;
        }
        #endregion
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
    public class CErzaDB
    {
        public string connection_string;
        private NpgsqlConnection npgsqlconn;
        public void Open()
        {
            this.npgsqlconn = new NpgsqlConnection(connection_string);
            this.npgsqlconn.Open();
        }
        public void Close()
        {
            this.npgsqlconn.Close();
        }
        public void CreateErzaDB()
        {
            StringBuilder create_query = new StringBuilder();
            create_query.Append("delimiter $$ ");
            create_query.Append("CREATE DATABASE `Erza` /*!40100 DEFAULT CHARACTER SET utf8 */$$");
            create_query.Append("delimiter $$ ");
            create_query.Append("CREATE TABLE `files` ( `image_id` bigint(20) NOT NULL,  `file_name` text NOT NULL) ENGINE=MyISAM DEFAULT CHARSET=utf8$$");
            create_query.Append("delimiter $$ ");
            create_query.Append("CREATE TABLE `image_tags` (  `tag_id` bigint(20) NOT NULL,  `image_id` bigint(20) NOT NULL) ENGINE=MyISAM DEFAULT CHARSET=utf8$$");
            create_query.Append("delimiter $$ ");
            create_query.Append("CREATE TABLE `images` (  `image_id` bigint(20) NOT NULL AUTO_INCREMENT,  `hash` tinyblob NOT NULL,  `is_new` bit(1) NOT NULL DEFAULT b'1',  `is_deleted` bit(1) NOT NULL DEFAULT b'0',  PRIMARY KEY (`image_id`),  UNIQUE KEY `index_hash` (`hash`(16)) USING BTREE) ENGINE=MyISAM AUTO_INCREMENT=2886190 DEFAULT CHARSET=utf8$$");
            create_query.Append("delimiter $$ ");
            create_query.Append("CREATE TABLE `tags` (  `tag_id` bigint(20) NOT NULL AUTO_INCREMENT,  `tag` varchar(255) NOT NULL,  PRIMARY KEY (`tag_id`),  UNIQUE KEY `index_tags` (`tag`)) ENGINE=MyISAM AUTO_INCREMENT=137721 DEFAULT CHARSET=utf8$$");
            using (NpgsqlCommand command = new NpgsqlCommand(create_query.ToString(), this.npgsqlconn))
            {
                command.ExecuteNonQuery();
            }
            return;
        }
        public void AddFileDB_MySql(long image_id, string new_file)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT * FROM files WHERE (file_name = @file_name)", this.npgsqlconn))
            {
                command.Parameters.AddWithValue("file_name", new_file);
                object o = command.ExecuteScalar();
                if (o == null)
                {
                    string ins = "INSERT INTO files (image_id, file_name) VALUES (@image_id, @file_name); select last_insert_rowid();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, this.npgsqlconn))
                    {

                        ins_command.Parameters.Add("file_name", NpgsqlTypes.NpgsqlDbType.Text, 4000).Value = new_file;
                        ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                        System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }
        }
        public void UpdateImageDB_MySql(CImage img)
        {
            string update = "UPDATE images SET is_new = @is_new, is_deleted = @is_deleted WHERE image_id = @image_id";
            using (NpgsqlCommand command = new NpgsqlCommand(update, this.npgsqlconn))
            {
                command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = img.image_id;
                command.Parameters.Add("is_new", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_new;
                command.Parameters.Add("is_deleted", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_deleted;
                command.ExecuteNonQuery();
            }
        }
        public void add_image_tags_MySql(long tag_id, long image_id)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT tag_id, image_id FROM image_tags WHERE (image_id = @image_id) AND (tag_id = @tag_id)", this.npgsqlconn))
            {
                command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                object o = command.ExecuteScalar();
                if (o == null)
                {
                    string ins = "INSERT INTO image_tags (tag_id, image_id) VALUES ( @tag_id, @image_id)";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, this.npgsqlconn))
                    {
                        ins_command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                        ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                        ins_command.ExecuteNonQuery();
                    }
                }
            }
        }
        public void add_image_tags_not_verify_MySql(long tag_id, long image_id)
        {
            string ins = "INSERT INTO image_tags (tag_id, image_id) VALUES ( @tag_id, @image_id)";
            using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, this.npgsqlconn))
            {
                ins_command.Parameters.Add("tag_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = tag_id;
                ins_command.Parameters.Add("image_id", NpgsqlTypes.NpgsqlDbType.Bigint).Value = image_id;
                ins_command.ExecuteNonQuery();
            }
        }
        public long AddTagDB_MySql(string tag)
        {
            long t;
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT tag_id FROM tags WHERE (tag = @tag)", this.npgsqlconn))
            {
                command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Text, 128).Value = tag;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    t = System.Convert.ToInt64(o);
                }
                else
                {
                    string ins = "INSERT INTO tags (tag) VALUES (@tag); SELECT LAST_INSERT_ID();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, this.npgsqlconn))
                    {
                        ins_command.Parameters.Add("tag", NpgsqlTypes.NpgsqlDbType.Text, 128).Value = tag;
                        t = System.Convert.ToInt64(ins_command.ExecuteScalar());
                    }
                }
            }

            return t;
        }
        public void AddImageDB_MySql(CImage img)
        {
            using (NpgsqlCommand command = new NpgsqlCommand("SELECT image_id FROM images WHERE (hash = @hash)", this.npgsqlconn))
            {
                command.Parameters.Add("hash", NpgsqlTypes.NpgsqlDbType.Text).Value = img.hash;
                object o = command.ExecuteScalar();
                if (o != null)
                {
                    img.image_id = System.Convert.ToInt64(o);
                    if (img.file != null)
                    {
                        AddFileDB_MySql(img.image_id, img.file);
                    }
                    //UpdateImageDB_MySql(img);
                    for (int i = 0; i < img.tags.Count; i++)
                    {
                        long tag_id = AddTagDB_MySql(img.tags[i]);
                        add_image_tags_MySql(tag_id, img.image_id);
                        //add_image_tags_not_verify_MySql(tag_id, image_id);
                    }
                }
                else
                {
                    string ins = "INSERT INTO images (hash, is_new, is_deleted) VALUES (@hash, @is_new, @is_deleted); SELECT LAST_INSERT_ID();";
                    using (NpgsqlCommand ins_command = new NpgsqlCommand(ins, this.npgsqlconn))
                    {
                        ins_command.Parameters.Add("hash", NpgsqlTypes.NpgsqlDbType.Text).Value = img.hash;
                        ins_command.Parameters.Add("is_new", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_new;
                        ins_command.Parameters.Add("is_deleted", NpgsqlTypes.NpgsqlDbType.Bit).Value = img.is_deleted;
                        long image_id = System.Convert.ToInt64(ins_command.ExecuteScalar());
                        for (int i = 0; i < img.tags.Count; i++)
                        {
                            long tag_id = AddTagDB_MySql(img.tags[i]);
                            add_image_tags_not_verify_MySql(tag_id, image_id);
                        }
                        /*if (img.tags.Count > 0)
                        {
                            List<long> tag_ids = get_tag_ids_MySql(img.tags);
                            add_image_tags_mass_not_verify_MySql(tag_ids, image_id);
                        }*/
                    }
                }
            }
            return;
        }
        public void add_image_tags_mass_not_verify_MySql(List<long> tag_ids, long image_id)
        {
            StringBuilder ins_quwery = new StringBuilder("INSERT INTO image_tags (tag_id, image_id) VALUES ");
            for (int i = 0; i < tag_ids.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
                else
                {
                    ins_quwery.Append(", ");
                    ins_quwery.Append("('");
                    ins_quwery.Append(tag_ids[i]);
                    ins_quwery.Append("','");
                    ins_quwery.Append(image_id);
                    ins_quwery.Append("')");
                }
            }
            ins_quwery.Append(";");
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), this.npgsqlconn))
            {
                command.ExecuteNonQuery();
            }
            return;
        }
        public List<long> get_tag_ids_MySql(List<string> tags)
        {
            List<long> ids = new List<long>();
            StringBuilder ins_quwery = new StringBuilder("SELECT tag_id FROM tags WHERE ");
            for (int i = 0; i < tags.Count; i++)
            {
                if (i == 0)
                {
                    ins_quwery.Append("tag = '");
                    ins_quwery.Append(tags[i]);
                    //tags[i] = tags[i].Replace("\\", "\\\\");
                    //ins_quwery.Append(tags[i].Replace("'", "\\'"));
                    ins_quwery.Append("'");
                }
                else
                {
                    ins_quwery.Append(" OR tag = '");
                    ins_quwery.Append(tags[i]);
                    //tags[i] = tags[i].Replace("\\", "\\\\");
                    //ins_quwery.Append(tags[i].Replace("'", "\\'"));
                    ins_quwery.Append("'");
                }
            }
            using (NpgsqlCommand command = new NpgsqlCommand(ins_quwery.ToString(), this.npgsqlconn))
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
    }
}
