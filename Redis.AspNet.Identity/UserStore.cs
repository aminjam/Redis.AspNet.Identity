using Microsoft.AspNet.Identity;
using ServiceStack.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;


namespace AspNet.Identity.Redis
{
    public enum Keys
    {
        AspNetUsers, UserLoginInfo
    }

    public class UserStore<TUser> : IUserLoginStore<TUser>, IUserClaimStore<TUser>, 
        IUserRoleStore<TUser>, IUserPasswordStore<TUser>, IUserSecurityStampStore<TUser>
        where TUser : IdentityUser
    {
        public static class RedisKey
        {
            public static string Build(Keys key, object entity = null, string id = null)
            {
                var sb = new StringBuilder(AppNamespace);
                switch (key)
                {
                    case Keys.AspNetUsers:
                        {
                            sb.Append("aspnetusers:");
                            break;
                        }
                    case Keys.UserLoginInfo:
                        {
                            sb.Append("userlogins:");
                            var userLogin = (UserLoginInfo)entity;
                            sb.Append(userLogin.LoginProvider + ":");
                            sb.Append(userLogin.ProviderKey + ":");
                            break;
                        }
                    default: break;
                }
                if (id != null)
                    sb.Append(id.ToString());
                return sb.ToString().TrimEnd(':');
            }
        }

        private bool _disposed;

        private IRedisClient db;
        public static string AppNamespace;

        public UserStore(IRedisClient _db)
        {
            db = _db;
        }

        #region Internal
        private void ThrowIfDisposed()
        {
            if (this._disposed)
                throw new ObjectDisposedException(this.GetType().Name);
        }
        public void Dispose()
        {
            this._disposed = true;
        }

        public void CheckDisposed(TUser user)
        {
            this.ThrowIfDisposed();
            if (user == null)
                throw new ArgumentNullException("user");
        }

        #endregion

        #region IUserLoginStore Implementation
        public Task AddLoginAsync(TUser user, UserLoginInfo login)
        {
            CheckDisposed(user);
            if (!user.Logins.Any(x => x.LoginProvider == login.LoginProvider && x.ProviderKey == login.ProviderKey))
            {
                user.Logins.Add(login);
            }

            return Task.FromResult(true);
        }

        public Task<TUser> FindAsync(UserLoginInfo login)
        {
            TUser user = null;
            var loginKeys = db.SearchKeys(RedisKey.Build(Keys.UserLoginInfo,entity: login));
            foreach (var loginKey in loginKeys)
            {
                var id = db.Get<string>(loginKey);
                if(id != string.Empty){
                    user = FindByIdAsync(id).Result;
                    if (user != null)
                        break;
                }
            }
            return Task.FromResult(user);
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user)
        {
            CheckDisposed(user);

            return Task.FromResult((IList<UserLoginInfo>)user.Logins);
        }

        public Task RemoveLoginAsync(TUser user, UserLoginInfo login)
        {
            CheckDisposed(user);

            user.Logins.RemoveAll(x => x.LoginProvider == login.LoginProvider && x.ProviderKey == login.ProviderKey);

            return Task.FromResult(0);
        }

        public Task CreateAsync(TUser user)
        {
            CheckDisposed(user);
            user = CreateOrUpdate(user);
            return Task.FromResult(user);
        }

        public TUser CreateOrUpdate(TUser user, string userId=null)
        {
            var baseKey = RedisKey.Build(Keys.AspNetUsers);
            if (string.IsNullOrEmpty(userId))
                user.Id = db.IncrementValue(baseKey).ToString();
            else
                user.Id = userId;
            var uniqueId = string.Format("{0}:{1}", user.UserName, user.Id);
            db.Add<TUser>(RedisKey.Build(Keys.AspNetUsers, id: uniqueId), user);

            //Add UserLogin Key-Value
            foreach (var login in user.Logins)
            {
                db.Add(RedisKey.Build(Keys.UserLoginInfo, entity: login), user.Id);
            }
            return user;
        }

        public Task DeleteAsync(TUser user)
        {
            CheckDisposed(user);
            var baseKey = RedisKey.Build(Keys.AspNetUsers);
            var uniqueKey = string.Format("{0}:{1}", user.UserName, user.Id);
            db.Remove(RedisKey.Build(Keys.AspNetUsers, id: uniqueKey));
            //Add UserLogin Key-Value
            foreach (var login in user.Logins)
            {
                var key = RedisKey.Build(Keys.UserLoginInfo, entity: login);
                if(db.ContainsKey(key))
                    db.Remove(key);
            }
            return Task.FromResult(true);
        }

        public Task<TUser> FindByIdAsync(string userId)
        {
            this.ThrowIfDisposed();
            var uniqueKey = string.Format("{0}:{1}", "*", userId);
            var keys = db.SearchKeys(RedisKey.Build(Keys.AspNetUsers, id: uniqueKey));
            if (keys.Any()) { uniqueKey = keys[0]; }
            var user = db.Get<TUser>(uniqueKey);
            return Task.FromResult(user);
        }

        public Task<TUser> FindByNameAsync(string userName)
        {
            this.ThrowIfDisposed();
            var uniqueKey = string.Format("{0}:{1}", userName, "*");
            var keys = db.SearchKeys(RedisKey.Build(Keys.AspNetUsers, id: uniqueKey));
            if (keys.Any()) { uniqueKey = keys[0]; }
            var user = db.Get<TUser>(uniqueKey);
            return Task.FromResult(user);
        }

        public Task UpdateAsync(TUser user)
        {
            CheckDisposed(user);
            DeleteAsync(user);
            return Task.FromResult(CreateOrUpdate(user,user.Id));
        }

        #endregion

        #region IUserClaimStore Implementation

        public Task AddClaimAsync(TUser user, System.Security.Claims.Claim claim)
        {
            CheckDisposed(user);

            if (!user.Claims.Any(x => x.ClaimType == claim.Type && x.ClaimValue == claim.Value))
            {
                user.Claims.Add(new IdentityUserClaim
                {
                    ClaimType = claim.Type,
                    ClaimValue = claim.Value
                });
            }

            return Task.FromResult(0);
        }

        public Task<IList<System.Security.Claims.Claim>> GetClaimsAsync(TUser user)
        {
            CheckDisposed(user);
            IList<Claim> result = user.Claims.Select(c => new Claim(c.ClaimType, c.ClaimValue)).ToList();
            return Task.FromResult(result);
        }

        public Task RemoveClaimAsync(TUser user, System.Security.Claims.Claim claim)
        {
            CheckDisposed(user);
            user.Claims.RemoveAll(x => x.ClaimType == claim.Type && x.ClaimValue == claim.Value);
            return Task.FromResult(0);
        }

        #endregion

        #region IUserPasswordStore Implementation
        public Task<string> GetPasswordHashAsync(TUser user)
        {
            CheckDisposed(user);
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(TUser user)
        {
            CheckDisposed(user);
            return Task.FromResult<bool>(user.PasswordHash != null);
        }

        public Task SetPasswordHashAsync(TUser user, string passwordHash)
        {
            CheckDisposed(user);
            user.PasswordHash = passwordHash;
            return Task.FromResult(0);
        }

        #endregion

        #region IUserSecurityStampStore Implementation
        public Task<string> GetSecurityStampAsync(TUser user)
        {
            CheckDisposed(user);
            return Task.FromResult(user.SecurityStamp);
        }

        public Task SetSecurityStampAsync(TUser user, string stamp)
        {
            CheckDisposed(user);
            user.SecurityStamp = stamp;
            return Task.FromResult(0);
        }

        #endregion


        #region IUserRoleStore Implementation
        public Task AddToRoleAsync(TUser user, string role)
        {
            CheckDisposed(user);
            if (!user.Roles.Contains(role, StringComparer.InvariantCultureIgnoreCase))
                user.Roles.Add(role);

            return Task.FromResult(true);
        }

        public Task<IList<string>> GetRolesAsync(TUser user)
        {
            CheckDisposed(user);
            return Task.FromResult<IList<string>>(user.Roles);
        }

        public Task<bool> IsInRoleAsync(TUser user, string role)
        {
            CheckDisposed(user);
            return Task.FromResult(user.Roles.Contains(role, StringComparer.InvariantCultureIgnoreCase));

        }

        public Task RemoveFromRoleAsync(TUser user, string role)
        {
            CheckDisposed(user);
            user.Roles.RemoveAll(r => String.Equals(r, role, StringComparison.InvariantCultureIgnoreCase));

            return Task.FromResult(0);
        }

        #endregion
    }
}
