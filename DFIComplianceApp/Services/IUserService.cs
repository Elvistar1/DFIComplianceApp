using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DFIComplianceApp.Models;

public interface IUserService
{
    void ToggleActive(Guid id);
    Task ToggleActiveAsync(Guid id);
    IEnumerable<User> All();
    Task<IEnumerable<User>> GetAllAsync();
    void Add(User u);
    Task AddAsync(User u);
    void Update(User u);
    Task UpdateAsync(User u);
    User? Authenticate(string username, string plainPassword, string role);
    Task<User?> GetByUsernameAsync(string username);
}
