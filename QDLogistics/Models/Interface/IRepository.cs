using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QDLogistics.Models
{
    public interface IRepository<TEntity> : IDisposable where TEntity : class
    {
        void Create(TEntity entity);

        void Update(TEntity entity);

        void Update(TEntity entity, params object[] keyValues);

        void Delete(TEntity entity);

        TEntity Get(int id);

        TEntity Get(string id);

        IEnumerable<TEntity> GetAll(bool noTracking = false);

        void SaveChanges();
    }
}
