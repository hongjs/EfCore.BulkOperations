using EfCore.BulkOperations.API.Models;
using EfCore.BulkOperations.API.Repositories;
using EfCore.BulkOperations.Test.Setup;

namespace EfCore.BulkOperations.Test;

public class EfCoreTest : BaseIntegrationTest
{
    private readonly IProductRepository _repo;

    public EfCoreTest(IntegrationTestFactory factory)
        : base(factory)
    {
        _repo = GetRequiredService<IProductRepository>();
    }

    [Fact]
    public async Task Should_RetrieveEmpty()
    {
        var products = await _repo.GetProducts();
        Assert.Empty(products);
    }

    [Fact]
    public async Task Should_RetrieveReturnNull()
    {
        var product = await _repo.GetProduct(Guid.NewGuid());
        Assert.Null(product);
    }

    [Fact]
    public async Task Should_InsertOne()
    {
        var items = new List<Product> { new("Test", 123.45m) };
        var count = await _repo.InsertProducts(items);
        Assert.Equal(1, count);

        var products = await _repo.GetProducts();
        Assert.Single(products);
    }

    [Fact]
    public async Task Should_InsertTwo()
    {
        var items = new List<Product>
        {
            new("Test1", 123.45m),
            new("Tes2t", 123.45m)
        };
        var count = await _repo.InsertProducts(items);
        Assert.Equal(2, count);

        var products = await _repo.GetProducts();
        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task Should_UpdateOne()
    {
        var items = new List<Product> { new("Test", 123.45m) };
        var count = await _repo.InsertProducts(items);
        Assert.Equal(1, count);

        var products = await _repo.GetProducts();
        Assert.Single(products);

        products[0].UpdateName("new name");
        var updateCount = await _repo.UpdateProducts(products);
        Assert.Equal(1, updateCount);

        products = await _repo.GetProducts();
        Assert.Single(products);
        Assert.Equal("new name", products[0].Name);
    }

    [Fact]
    public async Task Should_DeleteOne()
    {
        var items = new List<Product> { new("Test", 123.45m) };
        var count = await _repo.InsertProducts(items);
        Assert.Equal(1, count);

        var products = await _repo.GetProducts();
        Assert.Single(products);

        var deleteCount = await _repo.DeleteProducts(products);
        Assert.Equal(1, deleteCount);

        products = await _repo.GetProducts();
        Assert.Empty(products);
    }

    [Fact]
    public async Task Should_InsertOneUpdateOne()
    {
        var items = new List<Product> { new("Test", 123.45m) };
        var count = await _repo.InsertProducts(items);
        Assert.Equal(1, count);

        var products = await _repo.GetProducts();
        Assert.Single(products);

        products[0].UpdateName("new name");
        products.Add(new Product("new product", 123.45m));
        var mergeCount = await _repo.MergeProducts(products);
        Assert.Equal(3, mergeCount);

        products = await _repo.GetProducts();
        products = products.OrderBy(x => x.Name).ToList();
        Assert.Equal(2, products.Count);
        Assert.Equal("new name", products[0].Name);
        Assert.Equal("new product", products[1].Name);
    }

    public async Task Should_Committed()
    {
        var list1 = new List<Product> { new("Test1", 123.45m) };
        var list2 = new List<Product> { new("Test2", 123.45m) };
        await _repo.SyncDataThenCommit(list1, list2);

        var products = await _repo.GetProducts();
        Assert.Equal(2, products.Count);
    }

    public async Task Should_Rollbacked()
    {
        var list1 = new List<Product> { new("Test1", 123.45m) };
        var list2 = new List<Product> { new("Test2", 123.45m) };
        await _repo.SyncDataThenRollback(list1, list2);

        var products = await _repo.GetProducts();
        Assert.Empty(products);
    }
}