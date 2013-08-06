using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using Newtonsoft.Json;
/*
/**********************************************************************************
 * 
 * 如果文件的名称变小，将可能将其认为是最新的，默认文件名称只能越来越大
 * 超过指定数目将会删除名称最小的文件
 * 
**********************************************************************************
 */
namespace transferNewestFile
{
    class Program
    {
        static int MAX_FILE_COUNT = 5;
        static string src_file_path = @"C:\pics";
        static string dest_file_path = @"C:\picpng";
        static List<string> list_transfered_names = new List<string>();//已经处理过的图片的名称列表

        static void Main(string[] args)
        {
            //exportData();
            importData();
            Console.WriteLine("系统启动...");
            Console.WriteLine("源文件夹： " + src_file_path);
            Console.WriteLine("目标文件夹：" + dest_file_path);
            Console.WriteLine("最大缓存文件数： " + MAX_FILE_COUNT.ToString());

            Timer timer = new Timer(3000);
            timer.Elapsed += (sender, e) =>
            {
                start_loop();

            };
            timer.Enabled = true;

            //start_loop();
            //start_loop();
            Console.ReadLine();
        }
        static void start_loop()
        {
            DirectoryInfo TheFolder = new DirectoryInfo(src_file_path);

            FileInfo[] all_files = TheFolder.GetFiles();

            //找出每类图片的最新的一张
            List<string> list_newest_file
                = Find_newest_file_list(new List<FileInfo>(all_files), null, src_file_path);

            Debug.WriteLine("每类图片的最新的一张列表如下：");
            Display(list_newest_file, false);

            //查找是否已经处理过
            List<string> list_no_transfered_file
                = Find_not_transfered_file(list_newest_file, list_transfered_names);

            if (list_no_transfered_file.Count > 0)
            {
                Console.WriteLine("未处理的图片列表如下：");
                Debug.WriteLine("未处理的图片列表如下：");
                Display(list_no_transfered_file, true);
            }
            else
            {
                Debug.WriteLine("未发现未处理的图片");
            }

            //处理新找到的未处理文件并更新列表
            list_transfered_names
                = Act_on_file(list_no_transfered_file, list_transfered_names, src_file_path, dest_file_path);
        }
        static List<string> Act_on_file(List<string> _list_no_transfered_bmp,
                                               List<string> _list_transfered_names,
                                               string _src_file_path,
                                               string _des_file_path)
        {
            int len = _list_no_transfered_bmp.Count;
            if (len <= 0) return _list_transfered_names;

            string file_name = _list_no_transfered_bmp[0];
            // do act on file
            //删除目标文件夹中同类的文件
            List<string> des_file_list = Get_des_folder_existed_file(file_name, _des_file_path);
            if (des_file_list != null)
            {
                Delete_dest_folder_temp_files(des_file_list, _des_file_path);
            }
            string des_file_name = file_name + ".png";
            Debug.WriteLine("src_file:" + file_name);
            Debug.WriteLine("des_file:" + des_file_name);
            //将bmp转变为png存放到目标文件夹
            ImageFormatter(_src_file_path + "\\" + file_name, _des_file_path + "\\" + des_file_name);

            List<string> list_refreshed_transfered_names
                = Refresh_transfered_names_list(file_name, _list_transfered_names);

            List<string> list_next_loop = _list_no_transfered_bmp.GetRange(1, len - 1);

            return Act_on_file(list_next_loop, list_refreshed_transfered_names, _src_file_path, _des_file_path);
        }


        static void ImageFormatter(string sourcePath, string destationPath)
        {
            try
            {
                Bitmap bitmap = new Bitmap(sourcePath);
                bitmap.Save(destationPath, ImageFormat.Png);
                bitmap.Dispose();

                Console.WriteLine("已生成图片:" + destationPath);
                Debug.WriteLine("已生成图片:" + destationPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("转变为png时出现异常：" + ex.Message);
                Debug.WriteLine("转变为png时出现异常：" + ex.Message);
            }

        }
        static List<string> Get_des_folder_existed_file(string _file_name, string _file_path)
        {
            DirectoryInfo TheFolder = new DirectoryInfo(_file_path);
            FileInfo[] all_files = TheFolder.GetFiles();
            List<string> list_all_file_names = new List<FileInfo>(all_files).ConvertAll<string>(
                (_file_info) =>
                {
                    return _file_info.Name;
                });
            List<string> list_file_with_same_group = list_all_file_names.FindAll(
                (_file_temp) =>
                {
                    return Get_group_id(_file_temp) == Get_group_id(_file_name);
                });
            if (list_file_with_same_group.Count > 0)
            {
                return list_file_with_same_group;
            }
            else
            {
                return null;
            }
        }
        //刷新已经处理过的文件名列表  首先删除同类的名称，再添加新处理的文件的名称
        private static List<string> Refresh_transfered_names_list(string file_name,
                                                                  List<string> _list_transfered_names)
        {
            List<string> list_new = _list_transfered_names.GetRange(0, _list_transfered_names.Count);
            list_new.RemoveAll(
                (_name) =>
                {
                    return Get_group_id(_name) == Get_group_id(file_name);
                });
            list_new.Add(file_name);
            return list_new;
        }

        private static string Get_group_id(string file_name)
        {
            if (file_name.IndexOf("-") >= 0)
            {
                return file_name.Substring(0, file_name.IndexOf("-"));
            }
            else
                return string.Empty;
        }

        /// <summary>
        /// 查找未处理过的文件（,如果文件的名称变小，将可能将其认为是最新的）
        /// </summary>
        /// <param name="list_newest_bmp"></param>
        /// <param name="list_transfered_file_name"></param>
        /// <returns></returns>
        static List<string> Find_not_transfered_file(List<string> list_newest_bmp,
                                                     List<string> list_transfered_file_name)
        {
            return list_newest_bmp.FindAll(
                           (_name) =>
                           {
                               return !list_transfered_file_name.Exists(
                                    (_transerfered_name) =>
                                    {
                                        return _transerfered_name == _name;
                                    });
                           });
        }

        //找到不同类里面最新的文件
        static List<string> Find_newest_file_list(List<FileInfo> files,
                                                  List<string> _list_finded_newest_bmp,
                                                  string _file_path)
        {
            if (_list_finded_newest_bmp == null)
            {
                _list_finded_newest_bmp = new List<string>();
            }

            int totalCount = files.Count;
            if (totalCount > 0)
            {
                List<FileInfo> new_files = files.GetRange(1, totalCount - 1);


                List<string> list_files = Get_group_file_list(files);

                string newest_bmp = Get_newest_file_from_list(list_files, _file_path);
                if (!_list_finded_newest_bmp.Exists(
                    (_file_name) =>
                    {
                        return _file_name == newest_bmp;
                    }))
                {
                    List<string> list_finded_newest_bmp = new List<string>(_list_finded_newest_bmp);
                    list_finded_newest_bmp.Add(newest_bmp);

                    return Find_newest_file_list(new_files, list_finded_newest_bmp, _file_path);
                }
                else
                {
                    return Find_newest_file_list(new_files, _list_finded_newest_bmp, _file_path);
                }
            }
            else
            {
                return _list_finded_newest_bmp;
            }

        }

        private static string Get_newest_file_from_list(List<string> _list_bmps, string _file_path)
        {
            if (_list_bmps == null) return null;
            if (_list_bmps.Count == 0) return string.Empty;
            if (_list_bmps.Count == 1) return _list_bmps[0];

            List<string> list_bmps = new List<string>(_list_bmps);
            list_bmps.Sort((_first, _second) =>
             {
                 return string.Compare(_second, _first);
             });

            return Delete_src_folder_temp_files(list_bmps, _file_path)[0];
        }

        //删除目标文件夹中上次生成的同类图片
        private static void Delete_dest_folder_temp_files(List<string> des_file_list, string _des_file_path)
        {
            if (des_file_list.Count > 0)
            {
                string file_name = des_file_list[0];
                File.Delete(_des_file_path + "\\" + file_name);
                Delete_src_folder_temp_files(des_file_list.GetRange(0, des_file_list.Count - 1), _des_file_path);
            }
        }
        //删除存储过多的文件，返回经过排序的文件列表
        static List<string> Delete_src_folder_temp_files(List<string> _list_bmps, string _file_path)
        {
            //对数量过多进行处理
            if (_list_bmps.Count > MAX_FILE_COUNT)
            {
                string file_name = _list_bmps[_list_bmps.Count - 1];
                File.Delete(_file_path + "\\" + file_name);
                return Delete_src_folder_temp_files(_list_bmps.GetRange(0, _list_bmps.Count - 1), _file_path);
            }
            else
            {
                return _list_bmps;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        private static List<string> Get_group_file_list(List<FileInfo> files)
        {
            if (files.Count <= 0) return null;

            FileInfo fi = files[0];
            string file_name = fi.Name;
            string group_name = Get_group_id(file_name);

            //找到文件名前缀与group_name相同的文件
            List<FileInfo> list_files_in_group = files.FindAll(
                (_file) =>
                {
                    return Get_group_id(_file.Name) == group_name;
                });

            //将找到的文件列表的文件名作为列表返回
            return list_files_in_group.ConvertAll<string>(
                (_file) =>
                {
                    return _file.Name;
                });
        }//获取该组图片的文件名列表
        private static void Display(List<string> list, bool onConsole)
        {
            foreach (string s in list)
            {
                if (s == null)
                {
                    if (onConsole)
                    {
                        Console.WriteLine("(null)");
                    }
                    Debug.WriteLine("(null)");
                }
                else
                {
                    if (onConsole)
                    {
                        Console.WriteLine("\"{0}\"", s);
                    }
                    Debug.WriteLine("\"{0}\"", s);
                }
            }
            if (onConsole)
            {
                Console.WriteLine();
            }
        }
        static void importData()
        {
            string strReadFilePath1 = @"./config.txt";
            StreamReader srReadFile1 = new StreamReader(strReadFilePath1);
            string strConfig = srReadFile1.ReadToEnd();
            srReadFile1.Close();
            // eg. {"src_file_path":"C:\\Users\\ssor\\Desktop\\pics","dest_file_path":"C:\\Users\\ssor\\Desktop\\picpng","max_file_count":5}
            Debug.WriteLine(strConfig);
            Config cfg = (Config)JsonConvert.DeserializeObject<Config>(strConfig);
            if (cfg != null)
            {
                src_file_path = cfg.src_file_path;
                dest_file_path = cfg.dest_file_path;
                MAX_FILE_COUNT = cfg.max_file_count;
            }
        }
        static string exportData()
        {
            Config cfg = new Config(src_file_path, dest_file_path, MAX_FILE_COUNT);
            string output = JsonConvert.SerializeObject(cfg);

            return output;
        }
    }
    public class Config
    {
        public string src_file_path;
        public string dest_file_path;
        public int max_file_count;

        public Config(string _src_file_path, string _dest_file_path, int _max_file_count)
        {
            this.src_file_path = _src_file_path;
            this.dest_file_path = _dest_file_path;
            this.max_file_count = _max_file_count;
        }


    }
}
