﻿using MyStaging.App.Models;
using MyStaging.Common;
using MyStaging.Helpers;
using NpgsqlTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace MyStaging.App.DAL
{
    public class TablesDal
    {
        #region identity
        private string projectName = string.Empty;
        private string modelpath = string.Empty;
        private string schemaPath = string.Empty;
        private string dalpath = string.Empty;
        private string schemaName = string.Empty;
        private TableViewModel table = null;
        private List<FieldInfo> fieldList = new List<FieldInfo>();
        private List<PrimarykeyInfo> pkList = new List<PrimarykeyInfo>();
        private List<ConstraintInfo> consList = new List<ConstraintInfo>();
        #endregion

        public TablesDal(string projectName, string modelpath, string schemaPath, string dalpath, string schemaName, TableViewModel table)
        {
            this.projectName = projectName;
            this.modelpath = modelpath;
            this.schemaPath = schemaPath;
            this.dalpath = dalpath;
            this.schemaName = schemaName;
            this.table = table;
        }

        public void Create()
        {
            Get_Fields();
            Get_Primarykey(fieldList[0].Oid);
            Get_Constraint();

            CreateModel();
            CreateSchema();
            CreateDal();
        }

        public void CreateModel()
        {
            string _classname = CreateName() + "Model";
            string _fileName = $"{modelpath}/{_classname}.cs";
            using (StreamWriter writer = new StreamWriter(File.Create(_fileName), System.Text.Encoding.UTF8))
            {
                writer.WriteLine("using System;");
                writer.WriteLine("using System.Linq;");
                writer.WriteLine($"using {projectName}.DAL;");
                writer.WriteLine("using Newtonsoft.Json;");
                writer.WriteLine("using Newtonsoft.Json.Linq;");
                writer.WriteLine("using MyStaging.Mapping;");
                writer.WriteLine("using NpgsqlTypes;");
                writer.WriteLine("using MyStaging.Helpers;");
                writer.WriteLine();
                writer.WriteLine($"namespace {projectName}.Model");
                writer.WriteLine("{");
                writer.WriteLine($"\t[EntityMapping(name: \"{this.table.Name}\", Schema = \"{this.schemaName}\")]");
                writer.WriteLine($"\tpublic partial class {_classname}");
                writer.WriteLine("\t{");

                foreach (var item in fieldList)
                {
                    if (!string.IsNullOrEmpty(item.Comment))
                    {
                        writer.WriteLine("\t\t/// <summary>");
                        writer.WriteLine($"\t\t/// {item.Comment}");
                        writer.WriteLine("\t\t/// </summary>");
                    }
                    if (pkList.Count(f => f.Field == item.Field) > 0)
                        writer.WriteLine($"\t\t[PrimaryKey]");

                    string _type = item.RelType == "char" || item.RelType == "char?" ? "string" : item.RelType;
                    writer.WriteLine($"\t\tpublic {_type} {item.Field.ToUpperPascal()} {{ get; set; }}");
                    writer.WriteLine();
                }
                if (this.table.Type == "table")
                {
                    string dalPath = $"{ projectName }.DAL.";
                    Hashtable ht = new Hashtable();
                    foreach (var item in consList)
                    {
                        string f_dalName = CreateName(item.NspName, item.TablaName);
                        string pname = $"{item.TablaName.ToUpperPascal()}";
                        string propertyName = f_dalName;
                        if (ht.ContainsKey(propertyName) || _classname == propertyName)
                        {
                            propertyName += "By" + item.ConlumnName.ToUpperPascal();
                        }


                        string tmp_var = propertyName.ToLowerPascal();
                        writer.WriteLine($"\t\tprivate {f_dalName}Model {tmp_var} = null;");
                        writer.WriteLine($"\t\t[ForeignKeyMapping(name: \"{item.ConlumnName}\"), JsonIgnore] public {f_dalName}Model {propertyName} {{ get {{ if ({tmp_var} == null) {tmp_var} = {dalPath}{f_dalName}.Context.Where(f => f.{item.RefColumn.ToUpperPascal()} == this.{item.ConlumnName.ToUpperPascal()}).ToOne(); return {tmp_var}; }} }}");
                        writer.WriteLine();
                        ht.Add(propertyName, "");
                    }

                    List<string> cacheKey = new List<string>();
                    List<string> d_key = new List<string>();
                    foreach (var item in pkList)
                    {
                        FieldInfo fs = fieldList.FirstOrDefault(f => f.Field == item.Field);
                        d_key.Add("this." + fs.Field.ToUpperPascal());
                        var tostring = fs.CsType == "string" ? "" : ".ToString()";
                        cacheKey.Add($"this.{fs.Field.ToUpperPascal()}{tostring}");
                    }

                    string dalName = CreateName();
                    string updateName = $"{dalPath}{dalName}.{dalName}UpdateBuilder";
                    string dkString = d_key.Count > 0 ? $", { string.Join(",", d_key)}" : "";
                    string cacheString = cacheKey.Count > 0 ? $"{ string.Join(" + \"\" + ", cacheKey)}" : "";
                    writer.WriteLine($"\t\t[NonDbColumnMapping, JsonIgnore] public {updateName} UpdateBuilder {{ get {{ return new {updateName}(model =>{{MyStaging.Helpers.MyStagingUtils.CopyProperty<{_classname}>(this, model); PgSqlHelper.CacheManager?.RemoveItemCache<{_classname}>({cacheString}); }}{dkString}); }} }}");
                    writer.WriteLine();
                    writer.WriteLine($"\t\tpublic {_classname} Insert() {{ return {dalPath}{dalName}.Insert(this); }}");
                    writer.WriteLine();
                }
                writer.WriteLine("\t}");
                writer.WriteLine("}");
                writer.Flush();
            }
        }

        protected void CreateSchema()
        {
            string className = CreateName();
            string modelName = className + "Model";
            string schemaName = className + "Schema";

            string _fileName = $"{schemaPath}/{schemaName}.cs";
            using (StreamWriter writer = new StreamWriter(File.Create(_fileName), System.Text.Encoding.UTF8))
            {
                writer.WriteLine("using MyStaging.Common;");
                writer.WriteLine("using MyStaging.Helpers;");
                writer.WriteLine("using MyStaging.Schemas;");
                writer.WriteLine("using NpgsqlTypes;");
                writer.WriteLine("using System.Collections.Generic;");
                writer.WriteLine("using System.Reflection;");
                writer.WriteLine();
                writer.WriteLine($"namespace {projectName}.Model.Schemas");
                writer.WriteLine("{");
                writer.WriteLine($"\tpublic partial class {schemaName} : ISchemaModel");
                writer.WriteLine("\t{");
                writer.WriteLine($"\t\tpublic static {schemaName} Instance => new {schemaName}();");
                writer.WriteLine();
                writer.WriteLine($"\t\tprivate static Dictionary<string, SchemaModel> schemas {{ get; }}");
                writer.WriteLine();
                writer.WriteLine($"\t\tpublic Dictionary<string, SchemaModel> SchemaSet => schemas;");
                writer.WriteLine();
                writer.WriteLine($"\t\tprivate static List<PropertyInfo> properties;");
                writer.WriteLine();
                writer.WriteLine($"\t\tpublic List<PropertyInfo> Properties => properties;");
                writer.WriteLine();
                writer.WriteLine($"\t\tstatic {schemaName}()");
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\tschemas = new Dictionary<string, SchemaModel>");
                writer.WriteLine("\t\t\t{");
                for (int i = 0; i < fieldList.Count; i++)
                {
                    var fi = fieldList[i];
                    string ap = fi.Is_array ? " | NpgsqlDbType.Array" : "";
                    var pk = pkList.FirstOrDefault(f => f.Field == fi.Field) != null;
                    var primarykey = "";
                    if (pk)
                    {
                        primarykey = " ,Primarykey = true";
                    }
                    var type = fi.PgDbType.HasValue ? $" NpgsqlDbType.{fi.PgDbType}{ap}" : "null";
                    var line = $"{{\"{fi.Field}\", new SchemaModel{{ FieldName = \"{fi.Field}\", DbType = {type}, Size = {fi.Length}{primarykey}}} }}";
                    writer.WriteLine("\t\t\t\t" + line + (i + 1 == fieldList.Count ? "" : ","));
                }
                writer.WriteLine("\t\t\t};");
                writer.WriteLine($"\t\t\tproperties = ContractUtils.GetProperties(typeof({modelName}));");
                writer.WriteLine("\t\t}");
                writer.WriteLine("\t}");
                writer.WriteLine("}");
            }
        }

        private string CreateName(string schema, string tableName, string separator = "")
        {
            string _classname = string.Empty;
            if (schema == "public")
            {
                _classname = separator + tableName.ToUpperPascal();
            }
            else
            {
                _classname = $"{schema.ToUpperPascal()}{separator}{tableName.ToUpperPascal()}";
            }

            return _classname;
        }

        private string CreateName()
        {
            return CreateName(this.schemaName, this.table.Name);
        }

        protected void CreateDal()
        {
            string _classname = CreateName();
            string _model_classname = _classname + "Model";
            string _fileName = $"{dalpath}/{_classname}.cs";
            using (StreamWriter writer = new StreamWriter(File.Create(_fileName), System.Text.Encoding.UTF8))
            {
                writer.WriteLine("using System;");
                writer.WriteLine("using System.Linq;");
                writer.WriteLine("using Newtonsoft.Json;");
                writer.WriteLine("using Newtonsoft.Json.Linq;");
                writer.WriteLine("using MyStaging;");
                writer.WriteLine("using MyStaging.Helpers;");
                writer.WriteLine("using MyStaging.Common;");
                writer.WriteLine("using NpgsqlTypes;");
                writer.WriteLine("using System.Linq.Expressions;");
                writer.WriteLine("using System.Collections.Generic;");
                writer.WriteLine($"using {projectName}.Model;");
                writer.WriteLine($"using {projectName}.Model.Schemas;");
                writer.WriteLine();
                writer.WriteLine($"namespace {projectName}.DAL");
                writer.WriteLine("{");
                writer.WriteLine($"\tpublic partial class {_classname} : QueryContext<{_model_classname}>");
                writer.WriteLine("\t{");

                writer.WriteLine($"\t\tpublic static {_classname} Context {{ get {{ return new {_classname}(); }} }}");

                foreach (var item in fieldList)
                {
                    if (item.Is_array)
                    {
                        writer.WriteLine($"\t\tpublic {_classname} Where{item.Field.ToUpperPascal()}Any(params {item.RelType} {item.Field})");
                        writer.WriteLine("\t\t{");
                        writer.WriteLine($"\t\t\t if ({item.Field} == null || {item.Field}.Length == 0) return this;");
                        if (item.PgDbType.HasValue)
                        {
                            writer.WriteLine($"\t\t\t string text = JoinTo({item.Field}, NpgsqlDbType.{item.PgDbType}.ToString());");
                        }
                        else
                        {
                            writer.WriteLine($"\t\t\t string text = JoinTo({item.Field}, \"{item.Db_type}\");");
                        }
                        writer.WriteLine($"\t\t\t base.Where($\"{item.Field} @> array[{{text}}]\");");
                        writer.WriteLine($"\t\t\t return this;");
                        writer.WriteLine("\t\t}");
                        writer.WriteLine();
                    }
                }

                if (this.table.Type == "table")
                {
                    writer.WriteLine();
                    Insert_Generator(writer, _model_classname, _classname);
                    writer.WriteLine();
                    Delete_Generator(writer, _model_classname, _classname);
                    writer.WriteLine();
                    Update_Generator(writer, _model_classname, _classname);
                    writer.WriteLine();
                }

                writer.WriteLine("\t}");
                writer.WriteLine("}");
            }
        }

        protected void Insert_Generator(StreamWriter writer, string class_model, string className)
        {
            writer.WriteLine($"\t\tpublic static InsertBuilder<{class_model}> InsertBuilder => new InsertBuilder<{class_model}>({className}Schema.Instance);");
            writer.WriteLine($"\t\tpublic static {class_model} Insert({class_model} model) => InsertBuilder.Insert(model);");
            writer.WriteLine($"\t\tpublic static int InsertRange(List<{class_model}> models) => InsertBuilder.InsertRange(models).SaveChange();");
        }

        protected void Update_Generator(StreamWriter writer, string class_model, string dal_name)
        {
            List<string> d_key = new List<string>();
            List<string> d_key_fields = new List<string>();
            foreach (var item in pkList)
            {
                FieldInfo fs = fieldList.FirstOrDefault(f => f.Field == item.Field);
                d_key.Add(fs.RelType + " " + fs.Field);
                d_key_fields.Add(fs.Field);
            }

            string updateName = CreateName() + "UpdateBuilder";
            writer.WriteLine($"\t\tpublic static {updateName} UpdateBuilder => new {updateName}();");

            if (d_key.Count > 0)
            {
                writer.WriteLine($"\t\tpublic static {updateName} Update({string.Join(",", d_key)})");
                writer.WriteLine("\t\t{");
                writer.WriteLine($"\t\t\treturn new {updateName}(null, {string.Join(",", d_key_fields)});");
                writer.WriteLine("\t\t}");
                writer.WriteLine();
            }

            string dkString = d_key.Count > 0 ? $"{ string.Join(",", d_key)}" : "";
            var modelUpper = class_model.ToUpperPascal();
            writer.WriteLine($"\t\tpublic class {updateName} : UpdateBuilder<{modelUpper}>");
            writer.WriteLine("\t\t{");

            void CreateConstructor(string paramString, string onChange = null)
            {
                var baseT = onChange == null ? "" : " : base(onChanged)";
                writer.WriteLine($"\t\t\tpublic {updateName}({onChange}{dkString}){baseT}");
                writer.WriteLine("\t\t\t{");
                if (pkList.Count > 0)
                {
                    writer.Write($"\t\t\t\tbase.Where(f => ");
                    for (int i = 0; i < pkList.Count; i++)
                    {
                        var item = pkList[i];
                        writer.Write($"f.{item.Field.ToUpperPascal()} == {item.Field}");
                        if (i + 1 < pkList.Count)
                        {
                            writer.Write(" && ");
                        }
                    }
                    writer.Write(");\n");
                }
                writer.WriteLine("\t\t\t}");
            }
            // 默认构造函数
            CreateConstructor(dkString);
            writer.WriteLine();
            // 重载构造函数
            var separator = d_key.Count > 0 ? ", " : "";
            CreateConstructor(dkString, $"Action<{modelUpper}> onChanged" + separator);

            if (d_key.Count > 0)
            {
                writer.WriteLine();
                writer.WriteLine($"\t\t\tpublic {updateName}() {{ }}");
                writer.WriteLine();
            }

            writer.WriteLine($"\t\t\tpublic new {updateName} Where(Expression<Func<{class_model.ToUpperPascal()}, bool>> predicate)");
            writer.WriteLine("\t\t\t{");
            writer.WriteLine($"\t\t\t\tbase.Where(predicate);");
            writer.WriteLine($"\t\t\t\treturn this;");
            writer.WriteLine("\t\t\t}");

            writer.WriteLine($"\t\t\tpublic new {updateName} Where(string expression)");
            writer.WriteLine("\t\t\t{");
            writer.WriteLine($"\t\t\t\tbase.Where(expression);");
            writer.WriteLine($"\t\t\t\treturn this;");
            writer.WriteLine("\t\t\t}");

            foreach (var item in fieldList)
            {
                if (item.Is_identity) continue;

                writer.WriteLine($"\t\t\tpublic {updateName} Set{item.Field.ToUpperPascal()}({item.RelType} {item.Field})");
                writer.WriteLine("\t\t\t{");
                if (item.PgDbType.HasValue)
                {
                    string ap = item.Is_array ? " | NpgsqlDbType.Array" : "";
                    writer.WriteLine($"\t\t\t\tbase.SetField(\"{ item.Field}\", NpgsqlDbType.{item.PgDbType}{ap}, {item.Field}, {item.Length});");
                }
                else
                {
                    writer.WriteLine($"\t\t\t\tbase.SetField(\"{ item.Field}\", {item.Field}, {item.Length});");
                }

                writer.WriteLine($"\t\t\t\treturn this;");
                writer.WriteLine("\t\t\t}");

                if (item.Is_array)
                {
                    writer.WriteLine($"\t\t\tpublic {updateName} Set{item.Field.ToUpperPascal()}Append({item.CsType} {item.Field})");
                    writer.WriteLine("\t\t\t{");
                    if (item.PgDbType.HasValue)
                    {
                        writer.WriteLine($"\t\t\t\tbase.SetArrayAppend(\"{ item.Field}\", NpgsqlDbType.{item.PgDbType}, {item.Field}, {item.Length});");
                    }
                    else
                    {
                        writer.WriteLine($"\t\t\t\tbase.SetArrayAppend(\"{ item.Field}\", {item.Field}, {item.Length});");
                    }
                    writer.WriteLine($"\t\t\t\treturn this;");
                    writer.WriteLine("\t\t\t}");

                    writer.WriteLine($"\t\t\tpublic {updateName} Set{item.Field.ToUpperPascal()}Remove({item.CsType} {item.Field})");
                    writer.WriteLine("\t\t\t{");
                    if (item.PgDbType.HasValue)
                    {
                        writer.WriteLine($"\t\t\t\tbase.SetArrayRemove(\"{ item.Field}\", NpgsqlDbType.{item.PgDbType}, {item.Field}, {item.Length});");
                    }
                    else
                    {
                        writer.WriteLine($"\t\t\t\tbase.SetArrayRemove(\"{ item.Field}\", {item.Field}, {item.Length});");
                    }
                    writer.WriteLine($"\t\t\t\treturn this;");
                    writer.WriteLine("\t\t\t}");
                }
            }
            writer.WriteLine("\t\t}");
        }

        protected void Delete_Generator(StreamWriter writer, string class_model, string className)
        {
            string deletebuilder = $"DeleteBuilder<{class_model}>";
            writer.WriteLine($"\t\tpublic static {deletebuilder} DeleteBuilder => new {deletebuilder}();");

            if (pkList.Count > 0)
            {
                List<string> d_key = new List<string>();
                List<string> d_key_param = new List<string>();
                List<string> cacheKey = new List<string>();
                foreach (var item in pkList)
                {
                    FieldInfo fs = fieldList.FirstOrDefault(f => f.Field == item.Field);
                    d_key.Add(fs.RelType + " " + fs.Field);
                    d_key_param.Add("f." + fs.Field.ToUpperPascal() + " == " + fs.Field);

                    var tostring = fs.CsType == "string" ? "" : ".ToString()";
                    cacheKey.Add($"{fs.Field}{tostring}");
                }
                string cacheString = cacheKey.Count > 0 ? $"{ string.Join(" + \"\" + ", cacheKey)}" : "";
                writer.WriteLine($"\t\tpublic static int Delete({string.Join(",", d_key)})");
                writer.WriteLine("\t\t{");
                writer.WriteLine($"\t\t\tvar affrows = DeleteBuilder.Where(f => {string.Join(" && ", d_key_param)}).SaveChange();");
                writer.WriteLine($"\t\t\tif (affrows > 0) PgSqlHelper.CacheManager?.RemoveItemCache<{class_model}>({cacheString});");
                writer.WriteLine("\t\t\treturn affrows;");
                writer.WriteLine("\t\t\t}");
            }
        }

        #region primary key / constraint
        private void Get_Fields()
        {

            string _sqltext = @"SELECT a.oid
,c.attnum as num
,c.attname as field
, (case when f.character_maximum_length is null then c.attlen else f.character_maximum_length end) as length
,c.attnotnull as notnull
,d.description as comment
,(case when e.typcategory ='G' then e.typname when e.typelem = 0 then e.typname else e2.typname end) as type
,(case when e.typelem = 0 then e.typtype else e2.typtype end) as data_type
,e.typcategory
,f.is_identity
                                from  pg_class a 
                                inner join pg_namespace b on a.relnamespace=b.oid
                                inner join pg_attribute c on attrelid = a.oid
                                LEFT OUTER JOIN pg_description d ON c.attrelid = d.objoid AND c.attnum = d.objsubid and c.attnum > 0
                                inner join pg_type e on e.oid=c.atttypid
                                left join pg_type e2 on e2.oid=e.typelem
                                inner join information_schema.columns f on f.table_schema = b.nspname and f.table_name=a.relname and column_name = c.attname
                                WHERE b.nspname='{0}' and a.relname='{1}';";
            _sqltext = string.Format(_sqltext, this.schemaName, this.table.Name);


            PgSqlHelper.ExecuteDataReader(dr =>
            {
                FieldInfo fi = new FieldInfo();
                fi.Oid = Convert.ToInt32(dr["oid"]);
                fi.Field = dr["field"].ToString();
                fi.Length = Convert.ToInt32(dr["length"].ToString());
                fi.Is_not_null = Convert.ToBoolean(dr["notnull"]);
                fi.Comment = dr["comment"].ToString();
                fi.Data_Type = dr["data_type"].ToString();
                fi.Db_type = dr["type"].ToString();
                fi.Db_type = fi.Db_type.StartsWith("_") ? fi.Db_type.Remove(0, 1) : fi.Db_type;
                fi.PgDbType = PgsqlType.SwitchToSql(fi.Data_Type, fi.Db_type);
                fi.Is_identity = dr["is_identity"].ToString() == "YES";
                fi.Is_array = dr["typcategory"].ToString() == "A";
                fi.Is_enum = fi.Data_Type == "e";

                fi.CsType = PgsqlType.SwitchToCSharp(fi.Db_type);

                if (fi.Is_enum) fi.CsType = fi.CsType.ToUpperPascal();
                string _notnull = "";
                if (
                fi.CsType != "string"
                && fi.CsType != "byte[]"
                && fi.CsType != "JToken"
                && !fi.Is_array
                && fi.CsType != "System.Net.IPAddress"
                && fi.CsType != "System.Net.NetworkInformation.PhysicalAddress"
                && fi.CsType != "System.Xml.Linq.XDocument"
                && fi.CsType != "System.Collections.BitArray"
                && fi.CsType != "object"
                )
                    _notnull = fi.Is_not_null ? "" : "?";

                string _array = fi.Is_array ? "[]" : "";
                fi.RelType = $"{fi.CsType}{_notnull}{_array}";
                // dal
                this.fieldList.Add(fi);
            }, CommandType.Text, _sqltext);
        }

        protected void Get_Primarykey(int oid)
        {
            string _sqltext = $@"SELECT b.attname, format_type(b.atttypid, b.atttypmod) AS data_type
FROM pg_index a
INNER JOIN pg_attribute b ON b.attrelid = a.indrelid AND b.attnum = ANY(a.indkey)
WHERE a.indrelid = '{schemaName}.{table.Name}'::regclass AND a.indisprimary;
";
            PgSqlHelper.ExecuteDataReader(dr =>
            {
                PrimarykeyInfo pk = new PrimarykeyInfo();
                pk.Field = dr["attname"].ToString();
                pk.TypeName = dr["data_type"].ToString();
                pkList.Add(pk);
            }, CommandType.Text, _sqltext);
        }

        protected void Get_Constraint()
        {
            string _sqltext = string.Format(@"
SELECT(select attname from pg_attribute where attrelid = a.conrelid and attnum = any(a.conkey)) as conname
,b.relname,c.nspname,d.attname as ref_column,e.typname
FROM pg_constraint a 
left JOIN  pg_class b on b.oid= a.confrelid
inner join pg_namespace c on b.relnamespace = c.oid
INNER JOIN pg_attribute d on d.attrelid =a.confrelid and d.attnum=any(a.confkey)
inner join pg_type e on e.oid = d.atttypid
WHERE conrelid in 
(
SELECT a.oid FROM pg_class a 
inner join pg_namespace b on a.relnamespace=b.oid
WHERE b.nspname='{0}' and a.relname='{1}');"
        , this.schemaName, this.table.Name);


            PgSqlHelper.ExecuteDataReader(dr =>
                {
                    string conname = dr["conname"].ToString();
                    string contype = dr["typname"].ToString();
                    string ref_column = dr["ref_column"].ToString();
                    string relname = dr["relname"].ToString();
                    string nspname = dr["nspname"].ToString();
                    consList.Add(new ConstraintInfo()
                    {
                        ConlumnName = conname,
                        ConlumnType = contype,
                        RefColumn = ref_column,
                        TablaName = relname,
                        NspName = nspname
                    });
                }, CommandType.Text, _sqltext);
        }
        #endregion
    }
}
