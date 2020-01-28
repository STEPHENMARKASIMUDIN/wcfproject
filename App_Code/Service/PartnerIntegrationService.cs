using log4net.Config;

// NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service" in code, svc and config file together.
public sealed class PartnerIntegrationService : IPartnerIntegrationService
{
    private static DBConnection _DBConnection = null;
    private static readonly object padlock = new object();
 
    public PartnerIntegrationService()
    {
        XmlConfigurator.Configure();
        ConnectionInstance();
     
    }
    private void ConnectionInstance()
    {

        lock (padlock)
        {
            if (_DBConnection == null)
            {
                _DBConnection = new DBConnection(new ConnectionString());
            }
        }
    }
   
    public Response CheckConnection()
    {
        Response result = (Response)_DBConnection.CheckConnection();
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
    public LoginResponse PartnersLogin(Models.Login data)
    {
        LoginResponse result = (LoginResponse)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.GET, RequestType.PartnersLogin);
        return new LoginResponse { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage, loginData = result.loginData };
    }
    public Response PartnersRegistration(Models.PartnersData data)
    {
        Response result = (Response)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.POST, RequestType.PartnersRegistration);
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
    public Response PartnersUpdate(Models.PartnersData data)
    {
        Response result = (Response)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.POST, RequestType.PartnersUpdate);
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
    public LoginResponse AdminLogin(Models.Login data)
    {
        LoginResponse result = (LoginResponse)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.GET, RequestType.AdminLogin);
        return new LoginResponse { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage, adminData = result.adminData };
    }
    public Response ApproverRegistration(Models.Admin data)
    {
        Response result = (Response)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.POST, RequestType.ApproverRegistration);
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
    public ListOfApproverResponse ListOfApprover()
    {
        ListOfApproverResponse result = (ListOfApproverResponse)_DBConnection.DBConnect(new IntegrationProcess(), RequestType.ApproversList);
        return new ListOfApproverResponse { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage, adminList = result.adminList };
    }
    public Response ApproversUpdate(Models.Admin data)
    {
        Response result = (Response)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.POST, RequestType.ApproversUpdate);
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
    public DivisionListResponse DivisionList()
    {
        DivisionListResponse result = (DivisionListResponse)_DBConnection.DBConnect(new IntegrationProcess(), RequestType.DivisionList);
        return new DivisionListResponse { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage, divisionList = result.divisionList };
    }
    public PartnersListResponse PartnersList()
    {
        PartnersListResponse result = (PartnersListResponse)_DBConnection.DBConnect(new IntegrationProcess(), RequestType.PartnersList);
        return new PartnersListResponse { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage, partnersList = result.partnersList };
    }
    public Response ApprovePartner(Models.Approver data)
    {
        Response result = (Response)_DBConnection.DBConnect(new IntegrationProcess(), data, MethodType.POST, RequestType.ApprovePartner);
        return new Response { ResponseCode = result.ResponseCode, ResponsMessage = result.ResponsMessage };
    }
}
