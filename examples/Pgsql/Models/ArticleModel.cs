﻿using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MyStaging.Mapping;
using NpgsqlTypes;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pgsql.Model
{
	[Table(name: "article", Schema = "public")]
	public partial class ArticleModel
	{
		[PrimaryKey]
		public string id { get; set; }
		[PrimaryKey]
		public string userid { get; set; }
		public string title { get; set; }
		public JToken content { get; set; }
		public DateTime createtime { get; set; }
	}
}
