using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ProducerConsumer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Введіть максимальну місткість сховища:");
            int storageSize = int.Parse(Console.ReadLine());

            Console.WriteLine("Введіть загальну кількість продукції:");
            int totalItems = int.Parse(Console.ReadLine());

            Console.WriteLine("Введіть кількість виробників:");
            int producerCount = int.Parse(Console.ReadLine());

            Console.WriteLine("Введіть кількість споживачів:");
            int consumerCount = int.Parse(Console.ReadLine());

            new Starter().Start(storageSize, totalItems, producerCount, consumerCount);
            Console.WriteLine("Всі виробники та споживачі завершили свою роботу.");
        }
    }

    class Starter
    {
        private Semaphore _access;
        private Semaphore _emptySlots;
        private Semaphore _filledSlots;
        private static readonly object ConsoleLock = new object();

        public void Start(int storageSize, int totalItems, int producerCount, int consumerCount)
        {
            _access = new Semaphore(1, 1);                 // Взаємне виключення для доступу до сховища
            _emptySlots = new Semaphore(storageSize, storageSize); // Порожні слоти (спочатку всі)
            _filledSlots = new Semaphore(0, storageSize);          // Заповнені слоти (спочатку 0)

            Storage storage = new Storage(_access, _emptySlots, _filledSlots);

            List<Thread> producers = new List<Thread>();
            List<Thread> consumers = new List<Thread>();

            List<int> producerWorkload = DistributeWorkload(totalItems, producerCount);
            List<int> consumerWorkload = DistributeWorkload(totalItems, consumerCount);

            for (int i = 0; i < producerCount; i++)
            {
                int itemsToProduce = producerWorkload[i];
                int producerId = i + 1;
                var producer = new Thread(() => new Producer(storage, itemsToProduce, producerId, ConsoleLock).Produce());
                lock (ConsoleLock)
                {
                    Console.WriteLine($"[Створено виробника #{producerId}] Продукція для вироблення: {itemsToProduce}");
                }
                producer.Start();
                producers.Add(producer);
            }

            for (int i = 0; i < consumerCount; i++)
            {
                int itemsToConsume = consumerWorkload[i];
                int consumerId = i + 1;
                var consumer = new Thread(() => new Consumer(storage, itemsToConsume, consumerId, ConsoleLock).Consume());
                lock (ConsoleLock)
                {
                    Console.WriteLine($"[Створено споживача #{consumerId}] Продукція для споживання: {itemsToConsume}");
                }
                consumer.Start();
                consumers.Add(consumer);
            }

            producers.ForEach(t => t.Join());
            consumers.ForEach(t => t.Join());
        }

        private List<int> DistributeWorkload(int totalItems, int count)
        {
            List<int> workload = Enumerable.Repeat(totalItems / count, count).ToList();
            for (int i = 0; i < totalItems % count; i++)
            {
                workload[i]++;
            }
            return workload;
        }
    }

    class Storage
    {
        private readonly Queue<string> _storage = new Queue<string>();
        private readonly Semaphore _access;
        private readonly Semaphore _emptySlots;
        private readonly Semaphore _filledSlots;
        private static readonly object ConsoleLock = new object();

        public Storage(Semaphore access, Semaphore emptySlots, Semaphore filledSlots)
        {
            _access = access;
            _emptySlots = emptySlots;
            _filledSlots = filledSlots;
        }

        private void Log(string message, ConsoleColor color = ConsoleColor.Gray, int indentLevel = 0)
        {
            lock (ConsoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(new string(' ', indentLevel * 4) + message);
                Console.ResetColor();
            }
        }

        public void Add(string item, int producerId)
        {
            Log($"[Виробник #{producerId}] Очікування на порожнє місце у сховищі...", ConsoleColor.Yellow, 0);
            _emptySlots.WaitOne();

            Log($"[Виробник #{producerId}] Очікування доступу до сховища...", ConsoleColor.Yellow, 1);
            _access.WaitOne();

            _storage.Enqueue(item);

            Log($"[Виробник #{producerId}] Додано: {item} | Поточний обсяг: {_storage.Count}", ConsoleColor.Green, 2);

            _access.Release();
            _filledSlots.Release();
        }

        public string Take(int consumerId)
        {
            Log($"[Споживач #{consumerId}] Очікування на продукцію у сховищі...", ConsoleColor.Cyan, 0);
            _filledSlots.WaitOne();

            Log($"[Споживач #{consumerId}] Очікування доступу до сховища...", ConsoleColor.Cyan, 1);
            _access.WaitOne();

            string item = _storage.Dequeue();

            Log($"[Споживач #{consumerId}] Взято: {item} | Поточний обсяг: {_storage.Count}", ConsoleColor.Blue, 2);

            _access.Release();
            _emptySlots.Release();

            return item;
        }
    }

    class Producer
    {
        private readonly Storage _storage;
        private readonly int _itemsToProduce;
        private readonly int _id;
        private static int _globalProductId = 0;
        private static readonly object _idLock = new object();

        public Producer(Storage storage, int itemsToProduce, int id, object consoleLock)
        {
            _storage = storage;
            _itemsToProduce = itemsToProduce;
            _id = id;
        }

        public void Produce()
        {
            for (int i = 0; i < _itemsToProduce; i++)
            {
                int productId;
                lock (_idLock)
                {
                    productId = _globalProductId++;
                }
                string productName = $"Продукт_{productId} (Виробник #{_id})";
                _storage.Add(productName, _id);
                Thread.Sleep(500);
            }
        }
    }

    class Consumer
    {
        private readonly Storage _storage;
        private readonly int _itemsToConsume;
        private readonly int _id;

        public Consumer(Storage storage, int itemsToConsume, int id, object consoleLock)
        {
            _storage = storage;
            _itemsToConsume = itemsToConsume;
            _id = id;
        }

        public void Consume()
        {
            for (int i = 0; i < _itemsToConsume; i++)
            {
                _storage.Take(_id);
                Thread.Sleep(1500);
            }
        }
    }
}
