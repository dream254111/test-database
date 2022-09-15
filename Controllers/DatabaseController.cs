using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using System.Data.Common;
using TestDatabase.Extensions;
using TestDatabase.Models;
using TestDatabase.Models.Oracle.Query;
using TestDatabase.Models.Oracle.StoredProcedure;

namespace TestDatabase.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatabaseController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DatabaseController(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("oracle/sp")]
        public async Task<IActionResult> ExecuteOracleSP([FromQuery] OracleSPRequest request)
        {
            Validate validate = new Validate();

            /* CHECK MISSING IN QUERY STRING */
            if (string.IsNullOrEmpty(request.Name))
            {
                validate.IsMissing = true;
                validate.MissingError += "[name], ";
            }
            if (string.IsNullOrEmpty(request.Database))
            {
                validate.IsMissing = true;
                validate.MissingError += "[database], ";
            }
            if (validate.IsMissing)
            {
                return BadRequest(new Response<object>()
                {
                    Message = validate.MissingError.ValidateMessage()
                });
            }

            /* INPUT AND OUTPUT PARAMETERS */
            Dictionary<string, string> input = new Dictionary<string, string>();
            Dictionary<string, string> output = new Dictionary<string, string>();

            /* VALIDATE PARAMETERS */
            foreach (string entry in Request.Form.Keys)
            {
                StringValues value;
                if (Request.Form.TryGetValue(entry, out value))
                {
                    if (entry.StartsWith("PI_"))
                        input.Add(entry, value.First());
                    else if (entry.StartsWith("PO_"))
                        output.Add(entry, value.First());
                    else
                    {
                        validate.IsInvalid = true;
                        validate.InvalidError += $"[{entry}] - must begin with PI_ or PO_, ";
                    }
                }
            }

            string connectionString = _configuration.GetConnectionString(request.Database);
            if (string.IsNullOrEmpty(connectionString))
            {
                validate.IsInvalid = true;
                validate.InvalidError += "[database] - connection string not found, ";
            }

            if (validate.IsInvalid)
            {
                return BadRequest(new Response<object>()
                {
                    Message = validate.InvalidError.ValidateMessage()
                });
            }

            try
            {
                List<object> response = new List<object>();

                /* CONNECT TO DATABASE */
                using (OracleConnection connection = new OracleConnection(connectionString))
                {
                    connection.Open();

                    /* CREATE COMMAND THAT BIND BY NAME */
                    OracleCommand command = new OracleCommand(request.Name, connection)
                    {
                        CommandType = CommandType.StoredProcedure,
                        BindByName = true
                    };

                    /* ADD ALL PARAMETER IN */
                    foreach (KeyValuePair<string, string> entry in input)
                    {
                        command.Parameters.Add(new OracleParameter()
                        {
                            ParameterName = entry.Key,
                            Value = entry.Value,
                            Direction = ParameterDirection.Input,
                            OracleDbType = OracleDbType.NVarchar2,
                            Size = 4096
                        });
                    }

                    /* ADD ALL PARAMETER OUT */
                    List<OracleParameter> paramOut = new List<OracleParameter>();
                    foreach (KeyValuePair<string, string> entry in output)
                    {
                        /* IF VALUE IS TRUE, IT IS REF_CURSOR; OTHERWISE; STRING */
                        if (entry.Value == "true")
                        {
                            paramOut.Add(new OracleParameter()
                            {
                                ParameterName = entry.Key,
                                Direction = ParameterDirection.Output,
                                OracleDbType = OracleDbType.RefCursor
                            });
                        }
                        else
                        {
                            paramOut.Add(new OracleParameter()
                            {
                                ParameterName = entry.Key,
                                Direction = ParameterDirection.Output,
                                OracleDbType = OracleDbType.NVarchar2,
                                Size = 4096
                            });
                        }
                    }

                    command.Parameters.AddRange(paramOut.ToArray());

                    using (OracleDataReader reader = command.ExecuteReader())
                    {
                        /* EXECUTE ALL REF_CURSOR */
                        do
                        {
                            /* GET ALL COLUMN NAMES */
                            List<Dictionary<string, object>> records = new List<Dictionary<string, object>>();
                            List<string> columnList = new List<string>();
                            foreach (DbColumn entry in reader.GetColumnSchema())
                            {
                                columnList.Add(entry.ColumnName);
                            }

                            /* GET ALL DATA FROM COLUMN NAME */
                            while (reader.Read())
                            {
                                Dictionary<string, object> data = new Dictionary<string, object>();
                                foreach (string column in columnList)
                                {
                                    data.Add(column, reader.IsDBNull(column) ? "" : reader.GetString(column));
                                }
                                records.Add(data);
                            }

                            response.Add(records);
                        } while (reader.NextResult());

                        /* GET NON-REF_CURSOR DATA */
                        Dictionary<string, object> result = new Dictionary<string, object>();
                        foreach (OracleParameter entry in paramOut)
                        {
                            if (string.IsNullOrEmpty(entry.Value.ToString())) continue;
                            result.Add(entry.ParameterName.SPParamName(), entry.Value.ToString());
                        }
                        response.Add(result);
                    }
                }

                /* RETURN DATA BACK TO CLIENT */
                return Ok(new Response<object>()
                {
                    Success = true,
                    Data = response
                });
            }
            catch (Exception ex)
            {
                /* RETURN INTERNAL SERVER ERROR IN CASE OF UNDEFINED ERROR */
                return StatusCode(500, new Response<object>()
                {
                    Message = ex.ExceptionMessage()
                });
            }

        }

        [HttpGet("oracle/query")]
        public async Task<IActionResult> ExecuteOracleQuery([FromForm] OracleQueryRequest request)
        {
            Validate validate = new Validate();

            if (string.IsNullOrEmpty(request.Database))
            {
                validate.IsMissing = true;
                validate.MissingError += "[database], ";
            }
            if (string.IsNullOrEmpty(request.Query))
            {
                validate.IsMissing = true;
                validate.MissingError += "[query], ";
            }
            if (validate.IsMissing)
            {
                return BadRequest(new Response<object>()
                {
                    Message = validate.MissingError.ValidateMessage()
                });
            }

            string connectionString = _configuration.GetConnectionString(request.Database);
            if (string.IsNullOrEmpty(connectionString))
            {
                validate.IsInvalid = true;
                validate.InvalidError += "[database] - connection string not found, ";
            }
            if (validate.IsInvalid)
            {
                return BadRequest(new Response<object>()
                {
                    Message = validate.InvalidError.ValidateMessage()
                });
            }

            try
            {
                List<object> response = new List<object>();
                using (OracleConnection connection = new OracleConnection(connectionString))
                {
                    connection.Open();

                    OracleCommand command = new OracleCommand(request.Query, connection);

                    if (request.Query.StartsWith("SELECT"))
                    {
                        using (OracleDataReader reader = command.ExecuteReader())
                        {
                            List<string> columnList = new List<string>();
                            foreach (DbColumn entry in reader.GetColumnSchema())
                            {
                                columnList.Add(entry.ColumnName);
                            }

                            while (reader.Read())
                            {
                                Dictionary<string, object> data = new Dictionary<string, object>();
                                foreach (string column in columnList)
                                {
                                    data.Add(column, reader.IsDBNull(column) ? "" : reader.GetString(column));
                                }
                                response.Add(data);
                            }
                        }
                    }
                    else
                    {
                        response.Add(new
                        {
                            RowAffected = command.ExecuteNonQuery()
                        });
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new Response<object>()
                {
                    Message = ex.ExceptionMessage()
                });
            }
        }
    }
}
