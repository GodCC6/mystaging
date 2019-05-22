using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStaging.Common;
using MyStaging.Helpers;
using MyStaging.xUnitTest.DAL;
using MyStaging.xUnitTest.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace MyStaging.xUnitTest
{
    public class QueryContextTest
    {
        private readonly ITestOutputHelper output;
        public QueryContextTest(ITestOutputHelper output)
        {
            this.output = output;
            LoggerFactory factory = new LoggerFactory();
            var log = factory.CreateLogger<PgSqlHelper>();
            var options = new StagingOptions()
            {
                ConnectionMaster = ConstantUtil.CONNECTIONSTRING,
                ConnectionSlaves = new string[] { ConstantUtil.CONNECTIONSTRING },
                Logger = log
            };

            _startup.Init(options);
        }

        private string Sha256Hash(string text)
        {
            return Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        static int num = 0;
        [Fact]
        public void InsertTest()
        {
            for (int i = 0; i < 10; i++)
            {
                Thread thr = new Thread(new ThreadStart(() =>
                {
                    for (int j = 0; j < 1000; j++)
                    {
                        UserModel user = new UserModel()
                        {
                            Age = 18,
                            Createtime = DateTime.Now,
                            Id = ObjectId.NewId().ToString(),
                            Loginname = Guid.NewGuid().ToString("N").Substring(0, 8),
                            Money = 0,
                            Nickname = "������",
                            Password = Sha256Hash("123456"),
                            Sex = true
                        };
                        var result = User.Insert(user);
                        Assert.Equal(user.Id, result.Id);
                    }
                    num++;
                }))
                {
                    IsBackground = true
                };
                thr.Start();
            }
            while (num < 10)
            {
                Thread.Sleep(1000);
            }
        }

        [Fact]
        public void Insert()
        {
            UserModel user = new UserModel()
            {
                Age = 18,
                Createtime = DateTime.Now,
                Id = ObjectId.NewId().ToString(),
                Loginname = Guid.NewGuid().ToString("N").Substring(0, 8),
                Money = 0,
                Nickname = "������",
                Password = Sha256Hash("123456"),
                Sex = true
            };
            var result = User.Insert(user);
            Assert.Equal(user.Id, result.Id);
        }

        [Fact]
        public void ToList()
        {
            var context = User.Context;
            for (int i = 0; i < 10; i++)
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var user = context.Where(f => f.Id == "5cc69c04f6e262476805e111").ToOne();
                sw.Stop();

                this.output.WriteLine("Index:{0},Milli:{1}", i, sw.ElapsedMilliseconds);
            }
            return;
            var Createtime = context.ToScalar<DateTime>("Createtime");

            var list2 = User.Context.InnerJoin<ArticleModel>("b", (a, b) => a.Id == b.Userid).OrderByDescing(f => f.Createtime).Page(1, 10).ToList<UserViewModel>("a.id,a.nickname,a.password");

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                var t = Task.Run(() =>
                  {
                      Stopwatch sw = new Stopwatch();
                      sw.Start();
                      var result = User.Context.OrderByDescing(f => f.Createtime).Page(i, 10).ToList();
                      sw.Stop();

                      this.output.WriteLine("Index:{0},Milli:{1},Count:{2}", i, sw.ElapsedMilliseconds, result.Count);
                  });
                tasks.Add(t);

                Task.Run(() =>
                {
                    if (i >= 50 && i <= 55)
                    {
                        PgSqlHelper.Refresh(ConstantUtil.CONNECTIONSTRING, new string[] { ConstantUtil.CONNECTIONSTRING });
                    }
                });
            }

            Task.WaitAll(tasks.ToArray());

            Assert.Equal(1, 1);
        }

        public class UserViewModel
        {
            public string Id { get; set; }
            public string NickName { get; set; }
            public string Password { get; set; }
        }

        [Fact]
        public void ToListValueType()
        {
            var list = User.Context.OrderByDescing(f => f.Createtime).Page(1, 10).ToList<string>("id");

            Assert.Equal(10, list.Count);
        }

        [Fact]
        public void ToListValueTulpe()
        {
            var list = User.Context.OrderByDescing(f => f.Createtime).Page(1, 10).ToList<(string id, string loginname, string nickname)>("id", "loginname", "nickname");

            Assert.Equal(10, list.Count);
        }

        [Fact]
        public void ToOne()
        {
            string hash = Sha256Hash("123456");
            var user = User.Context.OrderBy(f => f.Createtime).ToOne();

            Assert.Equal(hash, user.Password);
        }

        [Fact]
        public void ToScalar()
        {
            string hash = Sha256Hash("123456");
            var password = User.Context.Where(f => f.Password == hash).OrderBy(f => f.Createtime).ToScalar<string>("password");

            Assert.Equal(hash, password);
        }

        [Fact]
        public void Sum()
        {
            int total = 360;
            // �Ȱ����ݿ�����������¼�޸�Ϊ 180 
            var age = User.Context.Where(f => f.Age == 180).Sum<long>(f => f.Age);

            Assert.Equal(total, age);
        }

        [Fact]
        public void Avg()
        {
            decimal avg = 180;
            // �Ȱ����ݿ�����������¼�� age �ֶ��޸�Ϊ 180 
            var age = User.Context.Where(f => f.Age == 180).Avg<decimal>(f => f.Age);

            Assert.Equal(avg, age);
        }

        [Fact]
        public void Count()
        {
            int count = 2;
            // �Ȱ����ݿ�����������¼�� age �ֶ��޸�Ϊ 180 
            var age = User.Context.Where(f => f.Age == 180).Count();

            Assert.Equal(count, age);
        }

        [Fact]
        public void Max()
        {
            int max = 180;
            var age = User.Context.Max<int>(f => f.Age);

            /// ����������ݿ�� age �ֶ��� 180
            Assert.Equal(max, age);
        }

        [Fact]
        public void Min()
        {
            int min = 18;
            var age = User.Context.Min<int>(f => f.Age);

            /// ����������ݿ�� age �ֶ��� 18
            Assert.Equal(min, age);
        }
    }
}
