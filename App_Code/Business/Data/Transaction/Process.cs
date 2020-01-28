using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for Process
/// </summary>
public abstract class Process
{
    public abstract IResponse IntegrationTransaction(MySqlConnection Connection, MySqlTransaction Transaction, IModel Model, RequestType RType);
    public abstract IResponse IntegrationTransaction(MySqlConnection Connection, IModel Model, RequestType RType);
    public abstract IResponse IntegrationTransaction(MySqlConnection Connection, RequestType Type);
   
}