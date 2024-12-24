//using BusinessLogic.authorization;
//using Domain.Interfaces;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using MapsterMapper;
//using BusinessLogic.Helpers;
//using Microsoft.Extensions.Options;
//using BusinessLogic.Models.Accounts;
//using Domain.Models;
//using Domain.Entities;
//using Microsoft.EntityFrameworkCore.Update;
//using System.Data;
//using System.Security.Cryptography;

//namespace BusinessLogic.Services
//{
//    public class AccountService : IAccountService
//    {
//        private readonly IRepositoryWrapper _repositoryWrapper;
//        private readonly IJwtUtils _jwtUtils;
//        private readonly IMapper _mapper;
//        private readonly AppSettings _appSettings;
//        private readonly IEmailService _emailService;

//        public AccountService(
//            IRepositoryWrapper repositoryWrapper,
//            IJwtUtils jwtUtils, 
//            IMapper mapper, 
//            IOptions<AppSettings> appSettings, 
//            IEmailService emailService)
//        {
//            _repositoryWrapper = repositoryWrapper;
//            _jwtUtils = jwtUtils;
//            _mapper = mapper;
//            _appSettings = appSettings.Value;
//            _emailService = emailService;
//        }


//        private void removeOldRefreshTokens(User account)
//        {
//            account.RefreshTokens.RemoveAll(x =>
//                !x.IsActive &&
//                x.Created.AddDays(_appSettings.RefreshTokenTTL) <= DateTime.UtcNow);
//        }

//        public async Task<AuthenticateResponse> Authenticate(AuthenticateRequest model, string ipAddress)
//        {
//            var account = await _repositoryWrapper.User.GetByEmailWithToken(model.Email);

//            if (account == null || !account.IsVerified || !BCrypt.Net.BCrypt.Verify(model.Password, account.PasswordHash))
//                throw new AppException("Email or password is incorrect");

//            var jwtToken = _jwtUtils.GenerateJwtToken(account);

//            // Дождитесь завершения задачи и получите RefreshToken
//            var refreshToken = await _jwtUtils.GenerateRefreshToken(ipAddress);

//            account.RefreshTokens.Add(refreshToken);

//            removeOldRefreshTokens(account);

//            await _repositoryWrapper.User.Update(account);
//            await _repositoryWrapper.Save();

//            var response = _mapper.Map<AuthenticateResponse>(account);
//            response.JwtToken = jwtToken;
//            return response;
//        }

//        public async Task<AccountResponse> Create(CreateRequest model)
//        {
//            if ((await _repositoryWrapper.User.FindCondition(x => x.Email == model.Email)).Count > 0)
//                throw new AppException($"Email '{model.Email}' is already registred");

//            var account = _mapper.Map<User>(model);
//            account.Created = DateTime.UtcNow;
//            account.Verified = DateTime.UtcNow;

//            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

//            await _repositoryWrapper.User.Update(account);
//            await _repositoryWrapper.Save();

//            return _mapper.Map<AccountResponse>(model);

//        }

//        public async Task Delete(int id)
//        {
//            var account = await getAccount(id);
//            await _repositoryWrapper.User.Delete(account);
//            await _repositoryWrapper.Save();
//        }

//        private async Task<string> generateResetToken()
//        {
//            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

//            var tokenIsUnique = (await _repositoryWrapper.User.FindCondition(x => x.ResetToken == token)).Count > 0;
//            if (!tokenIsUnique)
//                return await generateResetToken();

//            return token;
//        }

//        public async Task ForgotPassword(ForgotPasswordRequest model, string origin)
//        {
//            var account = (await _repositoryWrapper.User.FindCondition(x => x.Email == model.Email)).FirstOrDefault();

//            if (account == null) return;

//            account.ResetToken = await generateResetToken();
//            account.ResetTokenExpires = DateTime.UtcNow.AddDays(1);

//            await _repositoryWrapper.User.Update(account);
//            await _repositoryWrapper.Save();
//        }

//        public async Task<IEnumerable<AccountResponse>> GetAll()
//        {
//            var accounts = await _repositoryWrapper.User.FindAll(); 
//            return _mapper.Map<IList<AccountResponse>>(accounts);
//        }

//        public async Task<AccountResponse> GetById(int id)
//        {
//            var account = await getAccount(id);
//            return _mapper.Map<AccountResponse>(account);
//        }

//        public Task Register(RegisterRequest model, string origin)
//        {
//            throw new NotImplementedException();
//        }

//        public Task ResetPassword(ResetPasswordRequest model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task RevokeToken(string token, string ipAddress)
//        {
//            throw new NotImplementedException();
//        }

//        private async Task<User> getAccountByRefreshToken(string token)
//        {
//            var account = (await _repositoryWrapper.User.FindCondition(u => u.RefreshTokens.Any(t => t.Token == token))).SingleOrDefault();
//            if (account == null) throw new AppException("Invalid token");
//            return account; 
//        }

//        public async Task<AuthenticateResponse> RefreshToken(string token, string ipAddress)
//        {
//            var account = await getAccountByRefreshToken(token);
//            var refreshToken = account.RefreshTokens.Single(x => x.Token == token);

//            if (refreshToken.IsRevoked)
//            {
//                revokeDescendantRefreshTokens(refreshToken, account, ipAddress, $"Attempted reuse of revoked ancestor token: {token}");
//                await _repositoryWrapper.User.Update(account);
//                await _repositoryWrapper.Save();
//            }

//            if (!refreshToken.IsActive)
//                throw new AppException("Invalid token");

//            var newRefreshToken = await rotateRefreshToken(refreshToken, ipAddress);
//            account.RefreshTokens.Add(newRefreshToken);

//            removeOldRefreshTokens(account);

//            await _repositoryWrapper.User.Update(account);
//            await _repositoryWrapper.Save();

//            var jwtToken = _jwtUtils.GenerateJwtToken(account);

//            var response = _mapper.Map<AuthenticateResponse>(account);
//            response.JwtToken = jwtToken;
//            response.RefreshToken = newRefreshToken.Token;
//            return response;
//        }

//        private async Task<RefreshToken> rotateRefreshToken(RefreshToken refreshToken, string ipAddress)
//        {
//            var newRefreshToken = await _jwtUtils.GenerateRefreshToken(ipAddress);
//            revokeRefreshToken(refreshToken, ipAddress, "Replaced by new token", newRefreshToken.Token);
//            return newRefreshToken;
//        }

//        private void revokeRefreshToken(RefreshToken token, string ipAdress, string reason = null, string replacedByToken = null)
//        {
//            token.Revoked = DateTime.UtcNow;
//            token.RevokedByIp = ipAdress;
//            token.ReasonRevoked = reason;
//            token.ReplaceByToken = replacedByToken;
//        }

//        private void revokeDescendantRefreshTokens(RefreshToken refreshToken, User account, string ipAdress, string reason)
//        {
//            if (!string.IsNullOrEmpty(refreshToken.ReplaceByToken))
//            {
//                var childToken = account.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken.ReplaceByToken);
//                if (childToken.IsActive)
//                    revokeRefreshToken(childToken, ipAdress, reason);
//                else 
//                    revokeDescendantRefreshTokens(childToken, account, ipAdress, reason);
//            }
//        }

//        private async Task<User> getAccount(int id)
//        {
//            var account = (await _repositoryWrapper.User.FindCondition(x => x.Id == id)).FirstOrDefault();
//            if (account == null) throw new KeyNotFoundException("Account not find");
//            return account;
//        }

//        public async Task<AccountResponse> Update(int id, UpdateRequest model)
//        {

//            var account = await getAccount(id);

//            if (account.Email != model.Email && (await _repositoryWrapper.User.FindCondition(x => x.Email == model.Email)).Count > 0)
//                throw new AppException($"Email '{model.Email}' is already registered");

//            if (!string.IsNullOrEmpty(model.Password))
//                account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

//            _mapper.Map(model, account);
//            account.Updated = DateTime.UtcNow;
//            await _repositoryWrapper.User.Update(account);
//            await _repositoryWrapper.Save();

//            return _mapper.Map<AccountResponse>(account);
//        }

//        public Task ValidateResetToken(ValidateResetTokenRequest model)
//        {
//            throw new NotImplementedException();
//        }

//        public Task VerifyEmail(string token)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}