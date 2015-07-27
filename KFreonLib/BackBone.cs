using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KFreonLib
{
    // KFreon: Backbone factory class for queueing tasks threadsafely and allows things to be run before and after every job
    /// <summary>
    /// Backbone factory for queuing and running tasks threadsafely. Also allows things to be run before and after each job.
    /// </summary>
    public class BackBone
    {
        Queue<Task<bool>> JobList = new Queue<Task<bool>>();    // KFreon: List of jobs
        Func<bool> FirstHalf;                                   // KFreon: Function to run before each job
        Func<bool> LastHalf;                                    // KFreon: Function to run after each job
        readonly Object Locker = new object();


        /// <summary>
        /// Constructor for backbone. 
        /// </summary>
        /// <param name="First">Function to be run before each job.</param>
        /// <param name="Last">Function to be run after each job.</param>
        public BackBone(Func<bool> First, Func<bool> Last)
        {
            FirstHalf = First;
            LastHalf = Last;
        }


        /// <summary>
        /// Adds job to backbone.
        /// </summary>
        /// <param name="job">Job to be added to queue.</param>
        public void AddToBackBone(Func<bool, bool> job)
        {
            lock (Locker)
            {
                // KFreon: Start job if none in queue
                if (JobList.Count == 0)
                {
                    Task<bool> temp = new Task<bool>(() =>
                    {
                        FirstHalf();
                        bool retval = job(true);

                        // KFreon: Remove itself from queue
                        lock (Locker)
                            JobList.Dequeue();
                        LastHalf();
                        return retval;
                    });
                    JobList.Enqueue(temp);
                    temp.Start();
                }
                else
                {
                    // KFreon: Add job to queue
                    Task<bool> last = JobList.Last();
                    Task<bool> temp = last.ContinueWith(b =>
                    {
                        FirstHalf();
                        bool retval = job(b.Result);

                        // KFreon: Remove itself from queue
                        lock (Locker)
                            JobList.Dequeue();
                        LastHalf();
                        return retval;
                    });
                    JobList.Enqueue(temp);
                }
            }
        }

        public Task<bool> GetCurrentJob()
        {
            lock (Locker)
                return JobList.Peek();
        }
    }
}
