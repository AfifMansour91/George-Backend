//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using George.Common;

//namespace George.Services
//{
//	public static class RoleManager
//	{
//		//*********************  Data members/Constants  *********************//
//		private static readonly HashSet<string> _rolePermissions = new();
//		private static readonly List<RoleRes> _roles = new();

//		//**************************    Construction    **************************//
//		static RoleManager()
//		{
//			Init();
//		}


//		//*************************    Public Methods    *************************//
//		public static bool HasPermission(UserRole role, RolePermission permission)
//		{
//			string key = GenerateKey(role, permission);

//			return _rolePermissions.Contains(key);
//		}

//		public static List<RoleRes> GetRoles()
//		{
//			return _roles;
//		}


//		//*************************    Private Methods    *************************//
//		private static string GenerateKey(UserRole role, RolePermission permission)
//		{
//			return $"{(int)role}_{(int)permission}";
//		}

//		private static void AddPermission(UserRole role, RolePermission permission)
//		{
//			// Add to the HashSet.
//			_rolePermissions.Add(GenerateKey(role, permission));


//			// Add to the roles list.
//			RoleRes? roleRes = _roles.FirstOrDefault(a => a.Id == (int)role);
//			if (roleRes == null) 
//			{
//				roleRes = new() { Id = (int)role, Name = role.GetDescription() };
//				_roles.Add(roleRes);
//			}

//			roleRes.Permissions.Add(new() { Id = (int)permission, Name = permission.GetDescription() });
//		}

//		private static void Init()
//		{
//			UserRole role;

//			// SysOwner
//			role = UserRole.SysOwner;

//			AddPermission(role, RolePermission.AccountView);
//			AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			//AddPermission(role, RolePermission.AlertView);
//			//AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			AddPermission(role, RolePermission.SystemTaskEdit);

//			// SysAdmin
//			role = UserRole.SysAdmin;

//			AddPermission(role, RolePermission.AccountView);
//			AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			//AddPermission(role, RolePermission.AlertView);
//			//AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			AddPermission(role, RolePermission.SystemTaskEdit);

//			// SysTech
//			role = UserRole.SysTech;

//			AddPermission(role, RolePermission.AccountView);
//			AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			//AddPermission(role, RolePermission.AlertView);
//			//AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			AddPermission(role, RolePermission.SystemTaskEdit);

//			// ControlCenterManager
//			role = UserRole.ControlCenterManager;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			AddPermission(role, RolePermission.SystemTaskEdit);

//			// ControlCenterSupervisor
//			role = UserRole.ControlCenterSupervisor;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			AddPermission(role, RolePermission.SystemTaskEdit);

//			// ControlCenterOperator
//			role = UserRole.ControlCenterOperator;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// AccountAdmin
//			role = UserRole.AccountAdmin;

//			AddPermission(role, RolePermission.AccountView);
//			AddPermission(role, RolePermission.AccountViewSubscription);
//			AddPermission(role, RolePermission.AccountEdit);
//			AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// AccountManager
//			role = UserRole.AccountManager;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// AccountSupervisor
//			role = UserRole.AccountSupervisor;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// AccountOperator
//			role = UserRole.AccountOperator;

//			AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteAdmin
//			role = UserRole.SiteAdmin;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			AddPermission(role, RolePermission.SiteEdit);
//			AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteManager
//			role = UserRole.SiteManager;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			AddPermission(role, RolePermission.ShiftEdit);
//			AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteSupervisor
//			role = UserRole.SiteSupervisor;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteOperator
//			role = UserRole.SiteOperator;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteOperational
//			role = UserRole.SiteOperational;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			//AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//			// SiteCivilian
//			role = UserRole.SiteCivilian;

//			//AddPermission(role, RolePermission.AccountView);
//			//AddPermission(role, RolePermission.AccountViewSubscription);
//			//AddPermission(role, RolePermission.AccountEdit);
//			//AddPermission(role, RolePermission.AccountEditSubscription);
//			//AddPermission(role, RolePermission.SiteView);
//			//AddPermission(role, RolePermission.SiteEdit);
//			//AddPermission(role, RolePermission.SiteEditLimited);
//			//AddPermission(role, RolePermission.SiteAdminView_DELETE);
//			//AddPermission(role, RolePermission.SiteAdminEdit_DELETE);
//			//AddPermission(role, RolePermission.UserView);
//			//AddPermission(role, RolePermission.UserEdit);
//			//AddPermission(role, RolePermission.SystemSopView);
//			//AddPermission(role, RolePermission.SystemSopEdit);
//			//AddPermission(role, RolePermission.ShiftView);
//			//AddPermission(role, RolePermission.ShiftEdit);
//			//AddPermission(role, RolePermission.ShiftEditLimited);
//			//AddPermission(role, RolePermission.AlertOperatorEdit);
//			AddPermission(role, RolePermission.AlertView);
//			//AddPermission(role, RolePermission.AlertEdit);
//			//AddPermission(role, RolePermission.AlertOwnership);
//			//AddPermission(role, RolePermission.SystemTaskView);
//			//AddPermission(role, RolePermission.SystemTaskEdit);

//		}
//	}
//}
