﻿using MyStaging.Common;
using MyStaging.Interface;
using MyStaging.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace MyStaging.Core
{
    public abstract class ExpressionCondition<T>
    {
        /// <summary>
        ///  该方法没有对sql注入进行参数化过滤
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        public virtual void Where(string expression)
        {
            if (string.IsNullOrEmpty(expression)) throw new ArgumentNullException("必须传递参数 expression");

            WhereConditions.Add($"({expression})");
        }

        /// <summary>
        ///  增加查询条件
        /// </summary>
        /// <param name="formatCommad">格式为{0}{1}的字符串</param>
        /// <param name="pValue">{0}{1}对应的值</param>
        /// <returns></returns>
        public virtual void Where(string formatCommad, params object[] pValue)
        {
            if (pValue == null || pValue.Length == 0) throw new ArgumentNullException("必须传递参数 pValue");
            List<object> nameList = new List<object>();
            foreach (var item in pValue)
            {
                string name = Guid.NewGuid().ToString("N");
                this.AddParameter(name, item);
                nameList.Add("@" + name);
            }
            var expression = string.Format(formatCommad, nameList.ToArray());
            this.Where(expression);
        }

        /// <summary>
        ///  增加查询条件
        /// </summary>
        /// <param name="predicate">查询表达式</param>
        /// <returns></returns>
        public virtual void Where(Expression<Func<T, bool>> predicate) => Where<T>(null, predicate);

        /// <summary>
        ///  增加查询条件
        /// </summary>
        /// <typeparam name="TResult">查询表达式的对象</typeparam>
        /// <param name="predicate">查询表达式</param>
        /// <returns></returns>
        public virtual void Where<TResult>(Expression<Func<TResult, bool>> predicate) => this.Where<TResult>(null, predicate);

        /// <summary>
        ///  增加查询条件
        /// </summary>
        /// <typeparam name="TResult">查询表达式的对象</typeparam>
        /// <param name="alisName">alisName</param>
        /// <param name="predicate">查询表达式</param>
        /// <returns></returns>
        public virtual void Where<TResult>(string alisName, Expression<Func<TResult, bool>> predicate)
        {
            ExpressionInfo em = new ExpressionInfo
            {
                Body = predicate.Body,
                Model = typeof(TResult),
                UnionAlisName = alisName
            };
            WhereExpressions.Add(em);
        }

        /// <summary>
        ///  增加一个查询参数
        /// </summary>
        /// <param name="field">数据库字段</param>
        /// <param name="value">字段指定的值</param>
        /// <returns></returns>
        public abstract void AddParameter(string field, object value);

        /// <summary>
        /// 增加一组查询参数
        /// </summary>
        /// <param name="parameters">输入参数</param>
        /// <returns></returns>
        public virtual void AddParameter(params DbParameter[] parameters)
        {
            CheckNotNull.NotEmpty(parameters, nameof(parameters));
            Parameters.AddRange(parameters);
        }

        public void DeExpression()
        {
            if (WhereExpressions.Count > 0)
            {
                foreach (var item in WhereExpressions)
                {
                    DbExpressionVisitor expression = new DbExpressionVisitor();
                    expression.Visit(item.Body);
                    WhereConditions.Add(expression.SqlText.Builder.ToString().ToLower());
                    foreach (var p in expression.SqlText.Parameters)
                    {
                        AddParameter(p.Name, p.Value);
                    }
                }
            }
        }

        public TResult GetResult<TResult>(DbDataReader dr)
        {
            Type resultType = typeof(TResult);
            bool isEnum = resultType.IsEnum;

            TResult result;
            if (resultType == typeof(JsonElement))
            {
                result = (TResult)GetJsonElement(dr);
            }
            else if (IsValueType(resultType))
            {
                int columnIndex = -1;
                result = (TResult)GetValueTuple(resultType, dr, ref columnIndex);
            }
            else if (isEnum)
            {
                result = (TResult)GetValueType(resultType, dr);
            }
            else
            {
                result = Activator.CreateInstance<TResult>();
                var properties = resultType.GetProperties();
                foreach (var pi in properties)
                {
                    var value = dr[pi.Name];
                    if (value == DBNull.Value)
                        continue;
                    else if (pi.PropertyType.Name == "JsonElement")
                        pi.SetValue(result, JsonDocument.Parse(value.ToString()));
                    else
                        pi.SetValue(result, value);
                }
            }


            return result;
        }

        /// <summary>
        ///  检查查询结果对象是否为元组类型
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static bool IsValueType(Type type)
        {
            return (type.Namespace == "System" && type.Name.StartsWith("String")) || (type.BaseType == typeof(ValueType));
        }

        /// <summary>
        ///  从数据库流中读取值并转换为指定的对象类型
        /// </summary>
        /// <param name="objType">对象类型</param>
        /// <param name="dr">查询流</param>
        /// <returns></returns>
        public object GetValueType(Type objType, IDataReader dr)
        {
            object dbValue = dr[0];
            dbValue = dbValue is DBNull ? null : dbValue;
            dbValue = Convert.ChangeType(dbValue, objType);

            return dbValue;
        }

        /// <summary>
        ///  将查询结果转换为元组对象
        /// </summary>
        /// <param name="objType">元组类型</param>
        /// <param name="dr">查询流</param>
        /// <param name="columnIndex">dr index</param>
        /// <returns></returns>
        public object GetValueTuple(Type objType, IDataReader dr, ref int columnIndex)
        {
            bool isTuple = objType.Namespace == "System" && objType.Name.StartsWith("ValueTuple`");
            if (isTuple)
            {
                FieldInfo[] fs = objType.GetFields();
                Type[] types = new Type[fs.Length];
                object[] parameters = new object[fs.Length];
                for (int i = 0; i < fs.Length; i++)
                {
                    types[i] = fs[i].FieldType;
                    parameters[i] = GetValueTuple(types[i], dr, ref columnIndex);
                }
                ConstructorInfo info = objType.GetConstructor(types);
                return info.Invoke(parameters);
            }
            ++columnIndex;
            object dbValue = dr[columnIndex];
            dbValue = dbValue is DBNull ? null : dbValue;

            return dbValue;
        }

        /// <summary>
        ///  将查询结果转换为 JsonElement 对象
        /// </summary>
        /// <param name="dr">查询流</param>
        /// <returns></returns>
        public object GetJsonElement(IDataReader dr)
        {
            object dbValue = dr[0];
            if (dbValue is DBNull)
                return null;
            else
                return JsonDocument.Parse(dbValue.ToString()).RootElement;
        }

        public abstract string ToSQL();

        /// <summary>
        ///  清除参数列表
        /// </summary>
        public virtual void Clear()
        {
            this.Parameters.Clear();
            this.WhereConditions.Clear();
            this.WhereExpressions.Clear();
            this.CommandText = null;
        }

        /// <summary>
        ///  获取或者设置参数列表
        /// </summary>
        public List<DbParameter> Parameters { get; set; } = new List<DbParameter>();

        /// <summary>
        ///  获取或者设置查询表达式列表
        /// </summary>
        public List<ExpressionInfo> WhereExpressions { get; } = new List<ExpressionInfo>();

        /// <summary>
        ///  获取或者设置查询条件列表
        /// </summary>
        public List<string> WhereConditions { get; set; } = new List<string>();

        public string CommandText { get; set; }
    }
}
