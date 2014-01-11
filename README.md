Redis.AspNet.Identity
=====================

ASP.NET Identity provider that uses Redis for storage

## Purpose ##

ASP.NET MVC 5 shipped with a new Identity system (in the Microsoft.AspNet.Identity.Core package) in order to support both local login and remote logins via OpenID/OAuth, but only ships with an
Entity Framework provider (Microsoft.AspNet.Identity.EntityFramework).

## Features ##
* Drop-in replacement ASP.NET Identity with Redis as the backing store.
* Requires only 2 key-value types, while EntityFramework requires 5 tables
* Contains the same IdentityUser class used by the EntityFramework provider in the MVC 5 project template.
* Supports additional profile properties on your application's user model.
* Provides UserStore<TUser> implementation that implements the same interfaces as the EntityFramework version:
    * IUserStore<TUser>
    * IUserLoginStore<TUser>
    * IUserRoleStore<TUser>
    * IUserClaimStore<TUser>
    * IUserPasswordStore<TUser>
    * IUserSecurityStampStore<TUser>

## Instructions ##
These instructions assume you know how to set up Redis within an Web-API/MVC application.

1. Create a new ASP.NET MVC 5 project, choosing the Individual User Accounts authentication type.
2. Remove the Entity Framework packages and replace with Redis Identity:

```PowerShell
Uninstall-Package Microsoft.AspNet.Identity.EntityFramework
Uninstall-Package EntityFramework
Install-Package Redis.AspNet.Identity
```
  
3. In 
	~/Providers/ApplicationOAuthProvider.cs
	~/Controllers/AccountController.cs
	~/App_Start/Startup.Auth.cs
	
    * Remove the namespace: Microsoft.AspNet.Identity.EntityFramework
    * Add the namespace: Redis.AspNet.Identity
	
4. In ~/App_Start/Startup.Auth.cs
    * Remove the namespace: Microsoft.AspNet.Identity.EntityFramework
    * Add the new RedisClient with proper Connection String to the constructor of the UserStore.

```C#
static Startup(){
	...
	UserStore<IdentityUser>.AppNamespace = "urn:app:";
	UserManagerFactory = () => 
		new UserManager<IdentityUser>(
			new UserStore<IdentityUser>(
				new RedisClient("localhost",6379)));
}
```

Notes: When you have added your IoC, you should replace it here with an instance of IRedisClientsManager [More details on using client factories](http://www.piotrwalat.net/using-redis-with-asp-net-web-api/)

## Thanks To ##

Special thanks to [David Boike for RavenDB AspNet Identity](https://github.com/ILMServices/RavenDB.AspNet.Identity) and [InspectorIT for MongoDB.AspNet.Identity](https://github.com/InspectorIT/MongoDB.AspNet.Identity) that gave me the base for starting the Redis provider

