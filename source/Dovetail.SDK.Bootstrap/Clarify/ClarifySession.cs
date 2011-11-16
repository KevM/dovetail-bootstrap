using System;
using FChoice.Common.Data;
using FChoice.Foundation.Clarify;
using FChoice.Toolkits.Clarify.FieldOps;
using FChoice.Toolkits.Clarify.Interfaces;
using FChoice.Toolkits.Clarify.Support;

namespace Dovetail.SDK.Bootstrap.Clarify
{
    public interface IClarifySession
    {
        Guid Id { get; }
        string UserName { get; }
        int SessionEmployeeID { get; }
        int SessionUserID { get; }
        string SessionEmployeeSiteID { get; }
        ClarifyDataSet CreateDataSet();
        SupportToolkit CreateSupportToolkit();
        FieldOpsToolkit CreateFieldOpsToolkit();
        InterfacesToolkit CreateInterfacesToolkit();
        SqlHelper CreateSqlHelper(string sql);
    }

    public class ClarifySessionWrapper : IClarifySession
    {
        public ClarifySessionWrapper(ClarifySession clarifySession)
        {
            ClarifySession = clarifySession;
        }

        public ClarifySession ClarifySession { get; set; }

        public string SessionEmployeeSiteID
        {
            get { return Convert.ToString((object) ClarifySession["employee.site.site_id"]); }
        }

        public Guid Id
        {
            get { return ClarifySession.SessionID; }
        }

        
        public string UserName
        {
            get { return ClarifySession.UserName; }
        }

        public int SessionEmployeeID
        {
            get { return Convert.ToInt32((object) ClarifySession["employee.id"]); }
        }

        public int SessionUserID
        {
            get { return Convert.ToInt32((object) ClarifySession["user.id"]); }
        }

        public ClarifyDataSet CreateDataSet()
        {
            var clarifyDataSet = new ClarifyDataSet(ClarifySession);

            return clarifyDataSet;
        }

        public SupportToolkit CreateSupportToolkit()
        {
            return new SupportToolkit(ClarifySession);
        }

        public FieldOpsToolkit CreateFieldOpsToolkit()
        {
            return new FieldOpsToolkit(ClarifySession);
        }

        public InterfacesToolkit CreateInterfacesToolkit()
        {
            return new InterfacesToolkit(ClarifySession);
        }

        public SqlHelper CreateSqlHelper(string sql)
        {
            return new SqlHelper(sql);
        }
    }

}