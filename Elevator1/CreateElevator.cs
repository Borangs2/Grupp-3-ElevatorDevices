using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elevator1
{
    public  class CreateElevator
    {
        private readonly int _interval;
        public string StatusMessage { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();

        public CreateElevator(Guid id, string name, int interval)
        {
            Id = id;
            _interval = interval;
        }
        //TODO: Lägg til allt annat från program så att den skapar alla devices

        public  async Task CreteElevator()
        {
            StatusMessage = "Initializing device: -------";
            await Task.Delay(_interval);
            StatusMessage = "Initializing device: █------";
            await Task.Delay(_interval);
            StatusMessage = "Initializing device: ██-----";
            await Task.Delay(_interval);
            StatusMessage = "Initializing device: ███----";
            await Task.Delay(_interval);
            StatusMessage = "Initializing device: ████---";
            await Task.Delay(_interval);

        }
    }
}
