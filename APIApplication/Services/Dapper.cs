using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace APIApplication.Services
{
    public class Dapper : IDapper
    {
        private readonly IConfiguration _config;
        private string Connectionstring = "DefaultConnection";

        public Dapper(IConfiguration config) => _config = config;
        //public void Dispose()
        //{

        //}

        public int Execute(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            int i = 0;
            try
            {
                using (IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring)))
                {
                    i = db.Execute(sp, parms, commandType: commandType);
                }
            }
            catch (Exception ex)
            {
                i = -2;
            }
            return i;
        }

        public T Get<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.Text)
        {
            T result;
            try
            {
                using (IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring)))
                {
                    result = db.Query<T>(sp, parms, commandType: commandType).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return result;
        }
        public List<T> GetAll<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            using IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring));
            return db.Query<T>(sp, parms, commandType: commandType).ToList();
        }

        public DbConnection GetDbconnection()
        {
            return new SqlConnection(_config.GetConnectionString(Connectionstring));
        }

        public T Insert<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            T result;
            using (IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring)))
            {
                try
                {
                    using (var tran = db.BeginTransaction())
                    {
                        try
                        {
                            result = db.Query<T>(sp, parms, commandType: commandType, transaction: tran).FirstOrDefault();
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            throw ex;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            // using IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring));
            //try
            //{
            //    if (db.State == ConnectionState.Closed)
            //        db.Open();

            //    using var tran = db.BeginTransaction();
            //    try
            //    {
            //        result = db.Query<T>(sp, parms, commandType: commandType, transaction: tran).FirstOrDefault();
            //        tran.Commit();
            //    }
            //    catch (Exception ex)
            //    {
            //        tran.Rollback();
            //        throw ex;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    throw ex;
            //}
            //finally
            //{
            //    if (db.State == ConnectionState.Open)
            //        db.Close();
            //}
            return result;
        }

        public async Task<T> GetAsync<T>(string sp, DynamicParameters parms, CommandType commandType = CommandType.Text)
        {
            using (IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring)))
            {
                var result = await db.QueryAsync<T>(sp, parms, commandType: commandType);
                return result.FirstOrDefault();
            }
        }

        public async Task<int> ExecuteAsync(string sp, DynamicParameters parms, CommandType commandType = CommandType.StoredProcedure)
        {
            int i = 0;
            try
            {
                using (IDbConnection db = new SqlConnection(_config.GetConnectionString(Connectionstring)))
                {
                    i = await db.ExecuteAsync(sp, parms, commandType: commandType);
                }
            }
            catch (Exception ex)
            {
                i = -2;
            }
            return i;
        }
    }
}