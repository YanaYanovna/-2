using System;
using System.Collections.Generic;
using BookShop.Calculation;
using BookShop.Delivery;
using BookShop.Products;
using BookShop.Promo;
using System.Linq;
using BookShop.PromoCodes;

//+++++++++++++++Calculation
namespace BookShop.Calculation
{
    public class Cart
    {
        private readonly IDeliveryCalculator _deliveryCalculator;
        private readonly IPromoProvider _promoProvider;

        public Cart(IDeliveryCalculator deliveryCalculator, IPromoProvider promoProvider)
        {
            _deliveryCalculator = deliveryCalculator;
            _promoProvider = promoProvider;
        }

        public OrderInfo GetOrderInfo(List<IProduct> products, IPromoCode promoCode)
        {
            var orderInfo = new OrderInfo
            {
                Items = products.Select(p => new OrderItem { Product = p }).ToList(),
                DeliveryPrice = _deliveryCalculator.Calculate(products)
            };

            promoCode?.Apply(orderInfo);
            _promoProvider.GetActivePromos().ForEach(p => p.Apply(orderInfo));

            return orderInfo;
        }
    }

    public class OrderInfo
    {
        public List<OrderItem> Items { get; set; }
        public decimal DeliveryPrice { get; set; }
        public decimal Discount { get; set; }

        public decimal FinalProductPrice => Items.Sum(i => i.FinalPrice);
        public decimal TotalPrice => FinalProductPrice + DeliveryPrice - Discount;
    }

    public class OrderItem
    {
        public IProduct Product { get; set; }
        public decimal Discount { get; set; }
        public bool IsPromoApplied { get; set; }

        public decimal InitialPrice => Product.Price;
        public decimal FinalPrice => InitialPrice - Discount;
    }
}

//+++++++++++++++Delivery
namespace BookShop.Delivery
{
    public class DeliveryCalculator : IDeliveryCalculator
    {
        public decimal Calculate(List<IProduct> products)
        {
            var Count = products.OfType<Book>().Where(x => x.Type == BookType.Paper).Count();
            if (Count == 0)
                return 0;
            var sum = products.Sum(p => p.Price);
            if (sum < 1000)
                return 200;
            return 0;
        }

    }

    public interface IDeliveryCalculator
    {
        decimal Calculate(List<IProduct> products);
    }

}

//+++++++++++++++Products
namespace BookShop.Products
{
    public interface IProduct
    {
        string Name { get; set; }
        string Author { get; set; }
        Decimal Price { get; set; }
    }

    public class Book : IProduct
    {
        public string Name { get; set; }
        public string Author { get; set; }
        public Decimal Price { get; set; }
        public BookType Type { get; set; }
    }

    public enum BookType
    {
        Paper,
        Electronic
    }
}

//Promo
namespace BookShop.Promo
{
    public class TwoAuthorBooksPromo : IPromo
    {
        private readonly Book _electronicBook;

        public TwoAuthorBooksPromo(Book electronicBook)
        {
            if (electronicBook.Type != BookType.Electronic)
            {
                throw new ArgumentException("Book should be electronic");
            }

            _electronicBook = electronicBook;
        }

        public void Apply(OrderInfo orderInfo)
        {
            var paperBooks = orderInfo.Items.Where(p => !p.IsPromoApplied)
                .Where(oi => oi.Product.GetType() == typeof(Book))
                .Where(oi => ((Book)oi.Product).Type == BookType.Paper)
                .Take(2)
                .ToList();

            if (paperBooks.Count() < 2)
                return;

            paperBooks.ForEach(oi => oi.IsPromoApplied = true);

            orderInfo.Items.Add(new OrderItem()
            {
                Product = _electronicBook,
                Discount = _electronicBook.Price,
                IsPromoApplied = true
            });
        }
    }

    public interface IPromo
    {
        void Apply(OrderInfo orderInfo);
    }

    public interface IPromoProvider
    {
        List<IPromo> GetActivePromos();
    }

    public class PromoProvider : IPromoProvider
    {
        public List<IPromo> GetActivePromos()
        {
            return new List<IPromo>
            {
                new TwoAuthorBooksPromo(
                    new Book
                    {
                        Name = "Мастер и Маргарита",
                        Author = "Булгаков",
                        Price = 100,
                        Type = BookType.Electronic
                    }
                )
            };
        }
    }
}

//PromoCodes
namespace BookShop.PromoCodes
{
    public class FreeDeliveryPromoCode : IPromoCode
    {
        public void Apply(OrderInfo orderInfo)
        {
            orderInfo.Discount += orderInfo.DeliveryPrice;
        }
    }

    public class FreeBookPromoCode : IPromoCode
    {
        private readonly Book _book;

        public FreeBookPromoCode(Book book)
        {
            _book = book;
        }

        public void Apply(OrderInfo orderInfo)
        {
            var orderItem = orderInfo.Items.Where(p => !p.IsPromoApplied)
                .Where(oi => oi.Product.GetType() == typeof(Book))
                .FirstOrDefault(oi => oi.Product.Name == _book.Name && ((Book)oi.Product).Type == _book.Type);
            if (orderItem == null)
                return;

            orderItem.IsPromoApplied = true;
            orderItem.Discount = orderItem.FinalPrice;
        }
    }

    public interface IPromoCode
    {
        void Apply(OrderInfo orderInfo);
    }

    public class PercentDiscountPromoCode : IPromoCode
    {
        private readonly Decimal _percent;

        public PercentDiscountPromoCode(decimal percent)
        {
            if (_percent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percent));
            }

            _percent = percent;
        }

        public void Apply(OrderInfo orderInfo)
        {
            var discount = orderInfo.TotalPrice * _percent / 100;
            orderInfo.Discount += decimal.Round(discount, 2, MidpointRounding.AwayFromZero);
        }
    }

    public class SumDiscountPromoCode : IPromoCode
    {
        private decimal _sum;

        public SumDiscountPromoCode(decimal sum)
        {
            _sum = sum;
        }

        public void Apply(OrderInfo orderInfo)
        {
            orderInfo.Discount += Math.Min(_sum, orderInfo.TotalPrice);
        }
    }
}

namespace BookShop
{
    class Program
    {
        static void Main(string[] args)
        {
            var deliveryCalculator = new DeliveryCalculator();
            var promoProvider = new PromoProvider();
            var cart = new Cart(deliveryCalculator, promoProvider);

            var orderInfo = cart.GetOrderInfo(
                new List<IProduct>
                {
                    new Book
                    {
                        Name = "Book 1",
                        Author = "Булгаков",
                        Price = 100,
                        Type = BookType.Electronic
                    },
                    new Book
                    {
                        Name = "Book 2",
                        Author = "Булгаков",
                        Price = 300,
                        Type = BookType.Electronic
                    }
                }, null);
            Console.WriteLine(orderInfo.TotalPrice);
            orderInfo.Items.ForEach(item => Console.WriteLine(item.Product.Name));
        }
    }
}